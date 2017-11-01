/*
Copyright (C) 2011, 2016 Frank Stinner

This program is free software; you can redistribute it and/or modify it 
under the terms of the GNU General Public License as published by the 
Free Software Foundation; either version 3 of the License, or (at your 
option) any later version. 

This program is distributed in the hope that it will be useful, but 
WITHOUT ANY WARRANTY; without even the implied warranty of 
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General 
Public License for more details. 

You should have received a copy of the GNU General Public License along 
with this program; if not, see <http://www.gnu.org/licenses/>. 


Dieses Programm ist freie Software. Sie können es unter den Bedingungen 
der GNU General Public License, wie von der Free Software Foundation 
veröffentlicht, weitergeben und/oder modifizieren, entweder gemäß 
Version 3 der Lizenz oder (nach Ihrer Option) jeder späteren Version. 

Die Veröffentlichung dieses Programms erfolgt in der Hoffnung, daß es 
Ihnen von Nutzen sein wird, aber OHNE IRGENDEINE GARANTIE, sogar ohne 
die implizite Garantie der MARKTREIFE oder der VERWENDBARKEIT FÜR EINEN 
BESTIMMTEN ZWECK. Details finden Sie in der GNU General Public License. 

Sie sollten ein Exemplar der GNU General Public License zusammen mit 
diesem Programm erhalten haben. Falls nicht, siehe 
<http://www.gnu.org/licenses/>. 
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using GarminCore.Files.Typ;

namespace GarminCore.Files {

   /// <summary>
   /// Hauptklasse zur Behandlung von Garmin-Typfiles
   /// </summary>
   public class StdFile_TYP : StdFile {

      #region Header-Daten

      /// <summary>
      /// Basis für Tabellen
      /// </summary>
      abstract class DataTable {
         /// <summary>
         /// Offset und Länge der Tabelle in der Datei
         /// </summary>
         public DataBlock Data;
         /// <summary>
         /// Länge eines einzelnen Tabelleneintrages
         /// </summary>
         public UInt16 iItemLength;
         /// <summary>
         /// Anzahl der Tabelleneinträge
         /// </summary>
         public int iCount;

         protected void BaseRead(BinaryReaderWriter br) {
            Data.Offset = br.ReadUInt32();             // Offset Pointer to type data, offset block for POIS
            iItemLength = br.ReadUInt16();             // Denotes number of bytes for above pointer block ie 3 or 4
            Data.Length = br.ReadUInt32();             // Number of pois as a multiple of Poidata.iBytes
            iCount = iItemLength > 0 ? (int)Data.Length / iItemLength : 0;
         }

         protected void BaseWrite(BinaryReaderWriter bw) {
            bw.Write(Data.Offset);
            bw.Write(iItemLength);
            bw.Write(Data.Length);
         }

         public DataTable() {
            Data = new DataBlock();
            iItemLength = 0;
            iCount = 0;
         }
      }

      class POIDataTable : DataTable {

         public POIDataTable()
            : base() {
            iItemLength = 4;
         }

         public POIDataTable(BinaryReaderWriter br)
            : this() {
            BaseRead(br);
         }

         public void Write(BinaryReaderWriter bw) {
            BaseWrite(bw);
         }

         public override string ToString() {
            return string.Format("Data {0}, Bytes {1}, Count {2}", Data, iItemLength, iCount);
         }
      }

      class PolylineDataTable : DataTable {

         public PolylineDataTable()
            : base() {
            iItemLength = 4;
         }

         public PolylineDataTable(BinaryReaderWriter br)
            : this() {
            BaseRead(br);
         }

         public void Write(BinaryReaderWriter bw) {
            BaseWrite(bw);
         }

         public override string ToString() {
            return string.Format("Data 0x{0:x}, Bytes {1}, Count {2}", Data, iItemLength, iCount);
         }
      }

      class PolygoneDataTable : DataTable {

         public DataBlock DraworderBlock;
         public UInt16 iDraworderItemLength;

         public PolygoneDataTable()
            : base() {
            iItemLength = 4;
            iDraworderItemLength = 0;
            DraworderBlock = new DataBlock();
         }

         public PolygoneDataTable(BinaryReaderWriter br)
            : this() {
            BaseRead(br);

            DraworderBlock.Offset = br.ReadUInt32();  // Offset to Polygons draworder block ( generally 5 bytes for each polygon or start of new level)
            iDraworderItemLength = br.ReadUInt16();   // sets number of bytes for each polygon (ie 5) in Polygonedata.DraworderBlock
            DraworderBlock.Length = br.ReadUInt32();  // length of draworder block
         }

         public void Write(BinaryReaderWriter bw) {
            BaseWrite(bw);
            bw.Write(DraworderBlock.Offset);
            bw.Write(iDraworderItemLength);
            bw.Write(DraworderBlock.Length);
         }

         public override string ToString() {
            return string.Format("Data {0}, Bytes {1}, Count {2}, Draworder {3}, BytesForEachPolygon {4}",
               Data, iItemLength, iCount, DraworderBlock, iDraworderItemLength);
         }
      }

      DataBlock PointDatablock;
      DataBlock PolygoneDatablock;
      DataBlock PolylineDatablock;

      POIDataTable PointDatatable;
      PolylineDataTable PolylineDatatable;
      PolygoneDataTable PolygoneDatatable;

      /// <summary>
      /// Codepage, u.a.:
      /// <para>1250, Central & Eastern European</para>
      /// <para>1251, Cyrillic, mainly Slavic</para>
      /// <para>1252, West European</para>
      /// <para>1253, Greek</para>
      /// <para>1254, Turkish</para>
      /// <para>1255, Hebrew</para>
      /// <para>1256, Arabic</para>
      /// <para>1257, Baltic</para>
      /// <para>1258, Vietnamese</para>
      /// </summary>
      UInt16 _Codepage = 1252;
      /// <summary>
      /// Codepage für Texte (z.B. 1252)
      /// </summary>
      public UInt16 Codepage {
         get {
            return _Codepage;
         }
         set {
            if (value < 1250 || 1258 < value)
               throw new Exception("Die Codepage muß im Bereich 1250 bis 1258 sein");
            _Codepage = value;
         }
      }

      UInt16 _FamilyID = 0;
      /// <summary>
      /// zur eindeutigen Kennzeichnung für eine bestimmte Karte mit der gleichen ID
      /// </summary>
      public UInt16 FamilyID {
         get {
            return _FamilyID;
         }
         set {
            _FamilyID = value;
         }
      }

      UInt16 _ProductID = 1;
      /// <summary>
      /// Produkt-ID (i.A. 1)
      /// </summary>
      public UInt16 ProductID {
         get {
            return _ProductID;
         }
         set {
            _ProductID = value;
         }
      }

      POIDataTable NT_PointDatabtable;
      byte nt_unknown_0x65;
      DataBlock NT_PointDatablock;
      UInt32 nt_unknown_0x6E;
      DataBlock NT_PointLabelblock;
      UInt32 nt_unknown_0x7A;
      UInt32 nt_unknown_0x7E;
      DataBlock NT_LabelblockTable1;
      UInt32 nt_unknown_0x8A;
      UInt32 nt_unknown_0x8E;
      DataBlock NT_LabelblockTable2;
      UInt16 nt_unknown_0x9A;
      byte[] nt_unknown_0x9C = new byte[8];
      byte[] nt_unknown_0xA4 = new byte[10];
      byte[] nt_unknown_0xAE;

      public enum Headertyp {
         Unknown,
         Standard, // bis 0x5B
         NT_6E,
         NT_9C,
         NT_A4,
         NT_AE,
         NT_x,
      }

      Headertyp _HeaderTyp = Headertyp.Standard;
      public Headertyp HeaderTyp {
         get {
            return _HeaderTyp;
         }
         private set {
            _HeaderTyp = value;
            switch (_HeaderTyp) {
               case Headertyp.Standard:
                  Headerlength = 0x5b;
                  break;

               case Headertyp.NT_6E:
                  Headerlength = 0x6E;
                  nt_unknown_0x65 = 0x1f;
                  PointDatablock = new DataBlock();
                  PolygoneDatablock = new DataBlock();
                  PolylineDatablock = new DataBlock();
                  break;

               case Headertyp.NT_9C:
                  Headerlength = 0x9C;
                  nt_unknown_0x6E = nt_unknown_0x7A = nt_unknown_0x7E = nt_unknown_0x8A = nt_unknown_0x8E = nt_unknown_0x9A = 0;
                  PointDatablock = new DataBlock();
                  PolygoneDatablock = new DataBlock();
                  PolylineDatablock = new DataBlock();
                  break;

               case Headertyp.NT_A4:
                  Headerlength = 0xA4;
                  nt_unknown_0x6E = nt_unknown_0x7A = nt_unknown_0x7E = nt_unknown_0x8A = nt_unknown_0x8E = nt_unknown_0x9A = 0;
                  PointDatablock = new DataBlock();
                  PolygoneDatablock = new DataBlock();
                  PolylineDatablock = new DataBlock();
                  break;

               case Headertyp.NT_AE:
                  Headerlength = 0xAE;
                  nt_unknown_0x65 = 0x1f;
                  nt_unknown_0x6E = nt_unknown_0x7A = nt_unknown_0x7E = nt_unknown_0x8A = nt_unknown_0x8E = nt_unknown_0x9A = 0;
                  for (int i = 0; i < nt_unknown_0x9C.Length; i++)
                     nt_unknown_0x9C[i] = 0;
                  for (int i = 0; i < nt_unknown_0xA4.Length; i++)
                     nt_unknown_0xA4[i] = 0;
                  PointDatablock = new DataBlock();
                  PolygoneDatablock = new DataBlock();
                  PolylineDatablock = new DataBlock();
                  break;

               case Headertyp.NT_x:
                  if (Headerlength < 0xAE)
                     Headerlength = 0xAE;
                  nt_unknown_0x65 = 0x1f;
                  nt_unknown_0x6E = nt_unknown_0x7A = nt_unknown_0x7E = nt_unknown_0x8A = nt_unknown_0x8E = nt_unknown_0x9A = 0;
                  for (int i = 0; i < nt_unknown_0x9C.Length; i++)
                     nt_unknown_0x9C[i] = 0;
                  for (int i = 0; i < nt_unknown_0xA4.Length; i++)
                     nt_unknown_0xA4[i] = 0;
                  PointDatablock = new DataBlock();
                  PolygoneDatablock = new DataBlock();
                  PolylineDatablock = new DataBlock();
                  break;

               case Headertyp.Unknown:
                  break;

               default:
                  throw new Exception("Unbekannter Headertyp.");
            }
         }
      }

      #endregion

      enum InternalFileSections {
         PostHeaderData = 0,

         NT_PointDatablock,
         NT_PointDatabtable,
         NT_PointLabelblock,
         NT_LabelblockTable1,
         NT_LabelblockTable2,
      }

      protected SortedList<Polygone, byte> polygone;
      protected SortedList<Polyline, byte> polyline;
      protected SortedList<POI, byte> poi;

      /// <summary>
      /// Sammlung aller Fehler, die sich im Relaxed-Modus ergeben haben
      /// </summary>
      public string RelaxedModeErrors { get; private set; }

      /// <summary>
      /// liefert die Anzahl der Punkte in der internen Liste
      /// </summary>
      public int PoiCount { get { return poi.Count; } }
      /// <summary>
      /// liefert die Anzahl der Polygone in der internen Liste
      /// </summary>
      public int PolygonCount { get { return polygone.Count; } }
      /// <summary>
      /// liefert die Anzahl der Linien in der internen Liste
      /// </summary>
      public int PolylineCount { get { return polyline.Count; } }

      protected byte[] NTData;

      public StdFile_TYP()
         : base("TYP") {
         RelaxedModeErrors = "";

         HeaderTyp = Headertyp.Standard;
         Codepage = 1252;
         FamilyID = 0;
         ProductID = 1;

         PointDatablock = new DataBlock();
         PolygoneDatablock = new DataBlock();
         PolylineDatablock = new DataBlock();

         PointDatatable = new POIDataTable();
         PolylineDatatable = new PolylineDataTable();
         PolygoneDatatable = new PolygoneDataTable();

         // für NT-Format
         NT_PointDatabtable = new POIDataTable();
         NT_PointDatablock = new DataBlock();
         NT_PointLabelblock = new DataBlock();

         NT_LabelblockTable1 = new DataBlock();
         NT_LabelblockTable2 = new DataBlock();

         polygone = new SortedList<Polygone, byte>();
         polyline = new SortedList<Polyline, byte>();
         poi = new SortedList<POI, byte>();
      }

      public StdFile_TYP(BinaryReaderWriter br)
         : this(br, false) {
      }

      public StdFile_TYP(BinaryReaderWriter br, bool bRelaxed)
         : this() {
         nonvirtual_Read(br, bRelaxed);
      }

      public StdFile_TYP(StdFile_TYP tf)
         : this() {
         CreationDate = tf.CreationDate;
         Codepage = tf.Codepage;
         FamilyID = tf.FamilyID;
         ProductID = tf.ProductID;
         for (int i = 0; i < tf.PoiCount; i++)
            Insert(tf.GetPoi(i));
         for (int i = 0; i < tf.PolygonCount; i++)
            Insert(tf.GetPolygone(i));
         for (int i = 0; i < tf.PolylineCount; i++)
            Insert(tf.GetPolyline(i));
      }

      public override void ReadHeader(BinaryReaderWriter br) {
         base.ReadCommonHeader(br, Typ);

         if (Unknown_0x0C != 0x01) // Bedeutung unklar
            throw new Exception("Das ist keine Garmin-TYP-Datei.");

         Headertyp htyp = Headertyp.Unknown;

         Codepage = br.ReadUInt16();
         // Infos zu den Datenblöcken für POI, Polyline und Polygon einlesen (Offset, Länge)
         // (eigentlich uninteressant, da auf die Daten über die entsprechenden Tabellen zugegriffen wird)
         PointDatablock.Read(br);
         PolylineDatablock.Read(br);
         PolygoneDatablock.Read(br);

         FamilyID = br.ReadUInt16();
         ProductID = br.ReadUInt16();

         // Infos zu den Tabellen für POI, Polyline und Polygon einlesen (Offset, Länge, Länge der Tabelleneinträge)
         PointDatatable = new POIDataTable(br);
         PolylineDatatable = new PolylineDataTable(br);
         PolygoneDatatable = new PolygoneDataTable(br);

         htyp = Headertyp.Standard;

         // ev. kommt noch NT-Zeugs
         if (Headerlength > 0x5b) { // Extra icons
            htyp = Headertyp.NT_6E;

            // spez. Daten für NT1-Punkte
            NT_PointDatabtable = new POIDataTable(br);
            nt_unknown_0x65 = br.ReadByte();               // sollte wohl immer 0x1F sein (?), auch 0x0D
            NT_PointDatablock.Read(br);

            if (Headerlength > 0x6e) {           // Extra POI Labels
               htyp = Headertyp.NT_9C;

               nt_unknown_0x6E = br.ReadUInt32(); // 0
               NT_PointLabelblock.Read(br);       // Block-Offset und -Länge
               nt_unknown_0x7A = br.ReadUInt32(); // 6    Datensatzlänge?
               nt_unknown_0x7E = br.ReadUInt32(); // 0x1B
               NT_LabelblockTable1.Read(br);
               nt_unknown_0x8A = br.ReadUInt32(); // 6
               nt_unknown_0x8E = br.ReadUInt32(); // 0x1B
               NT_LabelblockTable2.Read(br);
               nt_unknown_0x9A = br.ReadUInt16(); // 0x12

               if (Headerlength > 0x9C) { // Indexing a selection of POIs
                  htyp = Headertyp.NT_A4;

                  br.ReadBytes(nt_unknown_0x9C); // scheint nochmal der gleiche Datenblock wie LabelblockTable2 zu sein

                  if (Headerlength > 0xA4) { // Active Routing
                     htyp = Headertyp.NT_AE;

                     br.ReadBytes(nt_unknown_0xA4);

                     if (Headerlength > 0xAE) {
                        htyp = Headertyp.NT_x;

                        nt_unknown_0xA4 = br.ReadBytes(Headerlength - (int)br.Position); // Rest einlesen

                     }
                  }
               }
            }
         }
         HeaderTyp = htyp;
      }

      protected override void ReadSections(BinaryReaderWriter br) {
         // --------- Dateiabschnitte für die Rohdaten bilden (nur NT) ---------
         Filesections.AddSection((int)InternalFileSections.NT_PointDatablock, new DataBlock(NT_PointDatablock));
         Filesections.AddSection((int)InternalFileSections.NT_PointDatabtable, new DataBlock(NT_PointDatabtable.Data));
         Filesections.AddSection((int)InternalFileSections.NT_PointLabelblock, new DataBlock(NT_PointLabelblock));
         Filesections.AddSection((int)InternalFileSections.NT_LabelblockTable1, new DataBlock(NT_LabelblockTable1));
         Filesections.AddSection((int)InternalFileSections.NT_LabelblockTable2, new DataBlock(NT_LabelblockTable2));
         if (GapOffset > HeaderOffset + Headerlength) // nur möglich, wenn extern z.B. auf den nächsten Header gesetzt
            Filesections.AddSection((int)InternalFileSections.PostHeaderData, HeaderOffset + Headerlength, GapOffset - (HeaderOffset + Headerlength));

         // Datenblöcke einlesen
         Filesections.ReadSections(br);

         SetSpecialOffsetsFromSections((int)InternalFileSections.PostHeaderData);

         br.SetEncoding(Codepage);
      }

      protected override void DecodeSections() {
         // Datenblöcke "interpretieren"
         int filesectiontype;

         filesectiontype = (int)InternalFileSections.NT_PointDatabtable;
         if (Filesections.GetLength(filesectiontype) > 0) {
            //Decode_NT_PointDatabtable(Filesections.GetSectionDataReader(filesectiontype), new DataBlock(0, Filesections.GetLength(filesectiontype)));
            //Filesections.RemoveSection(filesectiontype);
         }
         // usw.


      }

      /// <summary>
      /// liest die Daten in <see cref="polygone"/> ein
      /// </summary>
      /// <param name="br"></param>
      /// <param name="bRelaxed"></param>
      void Decode_PolygoneDatatable(BinaryReaderWriter br, bool bRelaxed) {
         if (PolygoneDatatable.iCount > 0) {
            StringBuilder sb = new StringBuilder();

            // Tabelle für Typen und Offsets zu den eigentlichen Daten einlesen
            List<TableItem> dataitem = new List<TableItem>();
            br.Seek(PolygoneDatatable.Data.Offset);
            for (int i = 0; i < PolygoneDatatable.iCount; i++)
               dataitem.Add(new TableItem(br, PolygoneDatatable.iItemLength));

            // Draworder-Tabelle einlesen
            List<PolygonDraworderTableItem> polygondraworder = new List<PolygonDraworderTableItem>();
            uint iLevel = 1;
            br.Seek(PolygoneDatatable.DraworderBlock.Offset);
            int blocklen = (int)PolygoneDatatable.DraworderBlock.Length;
            if (blocklen > 0)
               while (blocklen >= PolygoneDatatable.iDraworderItemLength) {
                  PolygonDraworderTableItem dro = new PolygonDraworderTableItem(br, PolygoneDatatable.iDraworderItemLength, iLevel);
                  blocklen -= PolygoneDatatable.iDraworderItemLength;
                  if (dro.Typ != 0)
                     polygondraworder.Add(dro);
                  else
                     iLevel++;
               }

            // Tabelle der Polygondaten einlesen
            polygone.Clear();
            for (int i = 0; i < dataitem.Count; i++) {
               br.Seek(dataitem[i].Offset + PolygoneDatablock.Offset);
               int datalen = i < dataitem.Count - 1 ?
                                    dataitem[i + 1].Offset - dataitem[i].Offset :
                                    (int)PolygoneDatatable.Data.Offset - (dataitem[i].Offset + (int)PolygoneDatablock.Offset);
               try {
                  long startpos = br.Position;
                  Polygone p = new Polygone(dataitem[i].Typ, dataitem[i].Subtyp);
                  p.Read(br);
                  Debug.WriteLineIf(startpos + datalen != br.Position,
                     string.Format("Diff. {0} der Datenlänge beim Lesen des Objektes 0x{1:x} 0x{2:x} (größer 0 bedeutet: zuviel gelesen)",
                                    br.Position - (startpos + datalen), dataitem[i].Typ, dataitem[i].Subtyp));
                  for (int j = 0; j < polygondraworder.Count; j++) {
                     if (p.Typ == polygondraworder[j].Typ)
                        for (int k = 0; k < polygondraworder[j].Subtypes.Count; k++)
                           if (p.Subtyp == polygondraworder[j].Subtypes[k]) {
                              p.Draworder = polygondraworder[j].Level;
                              j = polygondraworder.Count;       // 2. Schleifenabbruch
                              break;
                           }
                  }
                  polygone.Add(p, 0);
               } catch (Exception ex) {
                  if (bRelaxed) {
                     sb.AppendFormat("Fehler beim Einlesen von Polygon 0x{0:x2}, 0x{1:x2}: {2}", dataitem[i].Typ, dataitem[i].Subtyp, ex.Message);
                     sb.AppendLine();
                  } else
                     throw new Exception(ex.Message);
               }
            }
            if (bRelaxed)
               RelaxedModeErrors += sb.ToString();
         }
      }

      /// <summary>
      /// liest die Daten in <see cref="polyline"/> ein
      /// </summary>
      /// <param name="br"></param>
      /// <param name="bRelaxed"></param>
      void Decode_PolylineDatatable(BinaryReaderWriter br, bool bRelaxed) {
         if (PolylineDatatable.iCount > 0) {
            StringBuilder sb = new StringBuilder();

            // Tabelle für Typen und Offsets zu den eigentlichen Daten einlesen
            List<TableItem> dataitem = new List<TableItem>();
            br.Seek(PolylineDatatable.Data.Offset);
            for (int i = 0; i < PolylineDatatable.iCount; i++)
               dataitem.Add(new TableItem(br, PolylineDatatable.iItemLength));

            // Tabelle der Polylinedaten einlesen
            polyline.Clear();
            for (int i = 0; i < dataitem.Count; i++) {
               br.Seek(dataitem[i].Offset + PolylineDatablock.Offset);
               int datalen = i < dataitem.Count - 1 ?
                                    dataitem[i + 1].Offset - dataitem[i].Offset :
                                    (int)PolylineDatatable.Data.Offset - (dataitem[i].Offset + (int)PolylineDatablock.Offset);
               try {
                  long startpos = br.Position;
                  Polyline p = new Polyline(dataitem[i].Typ, dataitem[i].Subtyp);
                  p.Read(br);
                  Debug.WriteLineIf(startpos + datalen != br.Position,
                     string.Format("Diff. {0} der Datenlänge beim Lesen des Objektes 0x{1:x} 0x{2:x} (größer 0 bedeutet: zuviel gelesen)",
                                    br.Position - (startpos + datalen), dataitem[i].Typ, dataitem[i].Subtyp));
                  polyline.Add(p, 0);
               } catch (Exception ex) {
                  if (bRelaxed) {
                     sb.AppendFormat("Fehler beim Einlesen von Linie 0x{0:x2}, 0x{1:x2}: {2}", dataitem[i].Typ, dataitem[i].Subtyp, ex.Message);
                     sb.AppendLine();
                  } else
                     throw new Exception(ex.Message);
               }
            }
            if (bRelaxed)
               RelaxedModeErrors += sb.ToString();
         }
      }

      /// <summary>
      /// liest die Daten in <see cref="poi"/> ein
      /// </summary>
      /// <param name="br"></param>
      /// <param name="bRelaxed"></param>
      void Decode_POIDatatable(BinaryReaderWriter br, bool bRelaxed) {
         if (PointDatatable.iCount > 0) {
            StringBuilder sb = new StringBuilder();

            // Tabelle für Typen und Offsets zu den eigentlichen Daten einlesen
            List<TableItem> dataitem = new List<TableItem>();
            br.Seek(PointDatatable.Data.Offset);
            for (int i = 0; i < PointDatatable.iCount; i++)
               dataitem.Add(new TableItem(br, PointDatatable.iItemLength));

            // Tabelle der POI-Daten einlesen
            poi.Clear();
            for (int i = 0; i < dataitem.Count; i++) {
               br.Seek(dataitem[i].Offset + PointDatablock.Offset);
               int datalen = i < dataitem.Count - 1 ?
                                    dataitem[i + 1].Offset - dataitem[i].Offset :
                                    (int)PointDatatable.Data.Offset - (dataitem[i].Offset + (int)PointDatablock.Offset);
               try {
                  long startpos = br.Position;
                  POI p = new POI(dataitem[i].Typ, dataitem[i].Subtyp);
                  p.Read(br);
                  Debug.WriteLineIf(startpos + datalen != br.BaseStream.Position,
                     string.Format("Diff. {0} der Datenlänge beim Lesen des Objektes 0x{1:x} 0x{2:x} (größer 0 bedeutet: zuviel gelesen)",
                                    br.Position - (startpos + datalen), dataitem[i].Typ, dataitem[i].Subtyp));
                  poi.Add(p, 0);
               } catch (Exception ex) {
                  if (bRelaxed) {
                     sb.AppendFormat("Fehler beim Einlesen von Punkt 0x{0:x2}, 0x{1:x2}: {2}", dataitem[i].Typ, dataitem[i].Subtyp, ex.Message);
                     sb.AppendLine();
                  } else
                     throw new Exception(ex.Message);
               }
            }
            if (bRelaxed)
               RelaxedModeErrors += sb.ToString();
         }
      }

      /// <summary>
      /// zur Verwendung im Konstruktor
      /// </summary>
      /// <param name="br"></param>
      /// <param name="raw"></param>
      /// <param name="headeroffset"></param>
      /// <param name="gapoffset"></param>
      protected void nonvirtual_Read(BinaryReaderWriter br, bool raw = false, uint headeroffset = 0, uint gapoffset = 0) {
         base.Read(br, raw, headeroffset, gapoffset);

         br.SetEncoding(Codepage);

         RelaxedModeErrors = "";
         bool bRelaxed = true;

         Decode_PolygoneDatatable(br, bRelaxed);
         Decode_PolylineDatatable(br, bRelaxed);
         Decode_POIDatatable(br, bRelaxed);

      }
      
      public override void Read(BinaryReaderWriter br, bool raw = false, uint headeroffset = 0, uint gapoffset = 0) {
         nonvirtual_Read(br, raw, headeroffset, gapoffset);
      }

      protected override void Encode_Header(BinaryReaderWriter bw) {
         Unknown_0x0C = 0x01; // Bedeutung unklar
         base.Encode_Header(bw);

         bw.Write(Codepage);

         PointDatablock.Write(bw);
         PolylineDatablock.Write(bw);
         PolygoneDatablock.Write(bw);

         bw.Write(FamilyID);
         bw.Write(ProductID);

         PointDatatable.Write(bw);
         PolylineDatatable.Write(bw);
         PolygoneDatatable.Write(bw);

         if (Headerlength > 0x5b) {
            NT_PointDatabtable.Write(bw);
            bw.Write(nt_unknown_0x65);
            NT_PointDatablock.Write(bw);

            if (Headerlength > 0x6E) {
               bw.Write(nt_unknown_0x6E);
               NT_PointLabelblock.Write(bw);
               bw.Write(nt_unknown_0x7A);
               bw.Write(nt_unknown_0x7E);
               NT_LabelblockTable2.Write(bw);
               bw.Write(nt_unknown_0x8A);
               bw.Write(nt_unknown_0x8E);
               NT_LabelblockTable2.Write(bw);
               bw.Write(nt_unknown_0x9A);
               bw.Write(nt_unknown_0x9C);

               if (Headerlength > 0xA4) {
                  bw.Write(nt_unknown_0xA4);

                  if (Headerlength > 0xAE) {
                     nt_unknown_0xAE = new byte[Headerlength - 0xAE];
                     bw.Write(nt_unknown_0xAE);
                  }
               }
            }
         }
      }

      void Encode_PolygoneData(BinaryReaderWriter bw) {
         List<TableItem> table = new List<TableItem>();

         // ----- Polygonblock schreiben
         PolygoneDatatable.iCount = polygone.Count;
         // sollte besser aus der max. notwendigen Offsetgröße bestimmt werden (5 --> Offset max. 3 Byte)
         PolygoneDatatable.iItemLength = 5;
         PolygoneDatablock.Offset = (uint)bw.Position;
         foreach (Polygone p in polygone.Keys) {
            TableItem tableitem = new TableItem();
            tableitem.Typ = p.Typ;
            tableitem.Subtyp = p.Subtyp;
            tableitem.Offset = (int)(bw.Position - PolygoneDatablock.Offset);
            table.Add(tableitem);
            p.Write(bw, Codepage);
         }
         PolygoneDatablock.Length = (uint)bw.Position - PolygoneDatablock.Offset;

         // ----- Polygontabelle schreiben
         PolygoneDatatable.Data.Offset = (uint)bw.Position;    // Standort der Tabelle
         for (int i = 0; i < table.Count; i++)
            table[i].Write(bw, PolygoneDatatable.iItemLength);
         PolygoneDatatable.Data.Length = (uint)bw.Position - PolygoneDatatable.Data.Offset;
      }

      void Encode_PolylineData(BinaryReaderWriter bw) {
         List<TableItem> table = new List<TableItem>();

         PolylineDatatable.iCount = polyline.Count;
         // sollte besser aus der max. notwendigen Offsetgröße bestimmt werden (5 --> Offset max. 3 Byte)
         PolylineDatatable.iItemLength = 5;
         PolylineDatablock.Offset = (uint)bw.Position;
         table.Clear();
         foreach (Polyline p in polyline.Keys) {
            TableItem tableitem = new TableItem();
            tableitem.Typ = p.Typ;
            tableitem.Subtyp = p.Subtyp;
            tableitem.Offset = (int)(bw.Position - PolylineDatablock.Offset);
            table.Add(tableitem);
            p.Write(bw, Codepage);
         }
         PolylineDatablock.Length = (uint)bw.Position - PolylineDatablock.Offset;

         // ----- Polylinetabelle schreiben
         PolylineDatatable.Data.Offset = (uint)bw.Position;    // Standort der Tabelle
         for (int i = 0; i < table.Count; i++)
            table[i].Write(bw, PolylineDatatable.iItemLength);
         PolylineDatatable.Data.Length = (uint)bw.Position - PolylineDatatable.Data.Offset;
      }

      void Encode_POIData(BinaryReaderWriter bw) {
         List<TableItem> table = new List<TableItem>();

         // ----- POI-Block schreiben
         PointDatatable.iCount = poi.Count;
         // sollte besser aus der max. notwendigen Offsetgröße bestimmt werden (5 --> Offset max. 3 Byte)
         PointDatatable.iItemLength = 5;
         PointDatablock.Offset = (uint)bw.Position;
         table.Clear();
         foreach (POI p in poi.Keys) {
            TableItem tableitem = new TableItem();
            tableitem.Typ = p.Typ;
            tableitem.Subtyp = p.Subtyp;
            tableitem.Offset = (int)(bw.Position - PointDatablock.Offset);
            table.Add(tableitem);
            p.Write(bw, Codepage);
         }
         PointDatablock.Length = (uint)bw.Position - PointDatablock.Offset;

         // ----- POI-Tabelle schreiben
         PointDatatable.Data.Offset = (uint)bw.Position;    // Standort der Tabelle
         for (int i = 0; i < table.Count; i++)
            table[i].Write(bw, PointDatatable.iItemLength);
         PointDatatable.Data.Length = (uint)bw.Position - PointDatatable.Data.Offset;
      }

      void Encode_Draworder(BinaryReaderWriter bw) {
         // je Draworder eine Liste der Typen; je Typ eine Liste der Subtypes
         SortedList<uint, SortedList<uint, SortedList<uint, uint>>> draworderlist = new SortedList<uint, SortedList<uint, SortedList<uint, uint>>>();
         foreach (Polygone p in polygone.Keys) {
            SortedList<uint, SortedList<uint, uint>> typelist;
            if (!draworderlist.TryGetValue(p.Draworder, out typelist)) {
               typelist = new SortedList<uint, SortedList<uint, uint>>();
               draworderlist.Add(p.Draworder, typelist);
            }
            SortedList<uint, uint> subtypelist;
            if (!typelist.TryGetValue(p.Typ, out subtypelist)) {
               subtypelist = new SortedList<uint, uint>();
               typelist.Add(p.Typ, subtypelist);
            }
            subtypelist.Add(p.Subtyp, 0);

         }

         PolygoneDatatable.iDraworderItemLength = 5;
         PolygoneDatatable.DraworderBlock.Offset = (uint)bw.Position;
         uint olddraworder = 0;

         foreach (uint draworder in draworderlist.Keys) {
            while (olddraworder > 0 &&
                   draworder != olddraworder) {                  // Kennung für Erhöhung der Draworder schreiben
               new PolygonDraworderTableItem(0, 0).Write(bw, PolygoneDatatable.iDraworderItemLength);
               olddraworder++;
            }
            olddraworder = draworder;

            SortedList<uint, SortedList<uint, uint>> typelist = draworderlist[draworder];
            foreach (uint type in typelist.Keys) {                // für jeden Typ dieser Draworder einen Tabelleneintrag erzeugen
               PolygonDraworderTableItem ti = new PolygonDraworderTableItem(type, draworder);
               // ev. vorhandene Subtypes ergänzen
               SortedList<uint, uint> subtypelist = typelist[type];

               foreach (uint subtype in subtypelist.Keys)
                  ti.Subtypes.Add(subtype);
               ti.Write(bw, PolygoneDatatable.iDraworderItemLength);
            }
         }
         PolygoneDatatable.DraworderBlock.Length = (uint)bw.Position - PolygoneDatatable.DraworderBlock.Offset;
      }

      public override void Encode_Sections() {
         //SetData2Filesection((int)InternalFileSections.NT_PointDatabtable, true);

      }

      protected override void Encode_Filesection(BinaryReaderWriter bw, int filesectiontype) {
         //switch ((InternalFileSections)filesectiontype) {
         //   case InternalFileSections.NT_PointDatabtable:
         //      Encode_NT_PointDatabtable(bw);
         //      break;

         //}
      }

      public override void SetSectionsAlign() {
         // durch Pseudo-Offsets die Reihenfolge der Abschnitte festlegen
         uint pos = 0;
         Filesections.SetOffset((int)InternalFileSections.NT_PointDatablock, pos++);
         Filesections.SetOffset((int)InternalFileSections.NT_PointDatabtable, pos++);
         Filesections.SetOffset((int)InternalFileSections.NT_PointLabelblock, pos++);
         Filesections.SetOffset((int)InternalFileSections.NT_LabelblockTable1, pos++);
         Filesections.SetOffset((int)InternalFileSections.NT_LabelblockTable2, pos++);

         Filesections.AdjustSections(DataOffset);     // lückenlos ausrichten

         NT_PointDatabtable.Data = new DataBlock(Filesections.GetPosition((int)InternalFileSections.NT_PointDatabtable));
         NT_PointDatablock = new DataBlock(Filesections.GetPosition((int)InternalFileSections.NT_PointDatablock));
         NT_PointLabelblock = new DataBlock(Filesections.GetPosition((int)InternalFileSections.NT_PointLabelblock));
         NT_LabelblockTable1 = new DataBlock(Filesections.GetPosition((int)InternalFileSections.NT_LabelblockTable1));
         NT_LabelblockTable2 = new DataBlock(Filesections.GetPosition((int)InternalFileSections.NT_LabelblockTable2));
      }

      public override void Write(BinaryReaderWriter bw, uint headeroffset = 0, UInt16 headerlength = 0x5B, uint gapoffset = 0, uint dataoffset = 0, bool setsectiondata = true) {
         HeaderOffset = headeroffset;
         if (headerlength > 0)
            Headerlength = headerlength;

         CreationDate = DateTime.Now;

         GapOffset = gapoffset;
         DataOffset = dataoffset;

         bw.SetEncoding(Codepage);
         bw.Seek(Headerlength);

         Encode_PolygoneData(bw);
         Encode_PolylineData(bw);
         Encode_POIData(bw);
         Encode_Draworder(bw);

         SetSectionsAlign();

         Encode_Header(bw); // Header mit den akt. Offsets neu erzeugen

         Filesections.WriteSections(bw);

      }


      /// <summary>
      /// liefert einen POI aus der internen sortierten Liste; bei Fehler oder Nichtexistenz null
      /// </summary>
      /// <param name="idx"></param>
      /// <returns></returns>
      public POI GetPoi(int idx) {
         return (0 <= idx && idx < poi.Count) ? poi.Keys[idx] : null;
      }
      /// <summary>
      /// liefert einen POI aus der internen sortierten Liste; bei Fehler oder Nichtexistenz null
      /// </summary>
      /// <param name="typ"></param>
      /// <param name="subtyp"></param>
      /// <returns></returns>
      public POI GetPoi(uint typ, uint subtyp) {
         int idx = poi.IndexOfKey(new POI(typ, subtyp));
         return idx >= 0 ? GetPoi(idx) : null;
      }

      /// <summary>
      /// löscht einen POI aus der internen sortierte Liste
      /// </summary>
      /// <param name="idx"></param>
      /// <returns></returns>
      public bool RemovePoi(int idx) {
         if (0 <= idx && idx < poi.Count) {
            poi.RemoveAt(idx);
            return true;
         }
         return false;
      }
      public bool RemovePoi(uint typ, uint subtyp) {
         return RemovePoi(poi.IndexOfKey(new POI(typ, subtyp)));
      }

      /// <summary>
      /// liefert einen POI aus der internen sortierten Liste; bei Fehler oder Nichtexistenz null
      /// </summary>
      /// <param name="idx"></param>
      /// <returns></returns>
      public Polygone GetPolygone(int idx) {
         return (0 <= idx && idx < polygone.Count) ? polygone.Keys[idx] : null;
      }
      /// <summary>
      /// liefert einen POI aus der internen sortierten Liste; bei Fehler oder Nichtexistenz null
      /// </summary>
      /// <param name="typ"></param>
      /// <param name="subtyp"></param>
      /// <returns></returns>
      public Polygone GetPolygone(uint typ, uint subtyp) {
         int idx = polygone.IndexOfKey(new Polygone(typ, subtyp));
         return idx >= 0 ? GetPolygone(idx) : null;
      }

      /// <summary>
      /// löscht ein Polygone aus der internen sortierte Liste
      /// </summary>
      /// <param name="idx"></param>
      /// <returns></returns>
      public bool RemovePolygone(int idx) {
         if (0 <= idx && idx < polygone.Count) {
            polygone.RemoveAt(idx);
            return true;
         }
         return false;
      }
      public bool RemovePolygone(uint typ, uint subtyp) {
         return RemovePolygone(polygone.IndexOfKey(new Polygone(typ, subtyp)));
      }

      /// <summary>
      /// liefert einen POI aus der internen sortierten Liste; bei Fehler oder Nichtexistenz null
      /// </summary>
      /// <param name="idx"></param>
      /// <returns></returns>
      public Polyline GetPolyline(int idx) {
         return (0 <= idx && idx < polyline.Count) ? polyline.Keys[idx] : null;
      }
      /// <summary>
      /// liefert einen POI aus der internen sortierten Liste; bei Fehler oder Nichtexistenz null
      /// </summary>
      /// <param name="typ"></param>
      /// <param name="subtyp"></param>
      /// <returns></returns>
      public Polyline GetPolyline(uint typ, uint subtyp) {
         int idx = polyline.IndexOfKey(new Polyline(typ, subtyp));
         return idx >= 0 ? GetPolyline(idx) : null;
      }
      /// <summary>
      /// löscht ein Polyline aus der internen sortierte Liste
      /// </summary>
      /// <param name="idx"></param>
      /// <returns></returns>
      public bool RemovePolyline(int idx) {
         if (0 <= idx && idx < polyline.Count) {
            polyline.RemoveAt(idx);
            return true;
         }
         return false;
      }
      public bool RemovePolyline(uint typ, uint subtyp) {
         return RemovePolyline(polyline.IndexOfKey(new Polyline(typ, subtyp)));
      }

      /// <summary>
      /// löscht ein GraphicElement aus der entsprechenden internen Liste
      /// </summary>
      /// <param name="ge"></param>
      /// <returns></returns>
      public bool Remove(GraphicElement ge) {
         if (ge is Polygone)
            return polygone.Remove(ge as Polygone);
         if (ge is Polyline)
            return polyline.Remove(ge as Polyline);
         if (ge is POI)
            return poi.Remove(ge as POI);
         return false;
      }

      /// <summary>
      /// fügt ein GraphicElement in die entsprechende interne sortierte Liste ein; false, wenn es schon existiert
      /// </summary>
      /// <param name="p"></param>
      /// <returns></returns>
      public bool Insert(GraphicElement ge) {
         if (ge is Polygone) {
            if (polygone.ContainsKey(ge as Polygone))
               return false;
            else
               polygone.Add(ge as Polygone, 0);
            return true;
         }
         if (ge is Polyline) {
            if (polyline.ContainsKey(ge as Polyline))
               return false;
            else
               polyline.Add(ge as Polyline, 0);
            return true;
         }
         if (ge is POI) {
            if (poi.ContainsKey(ge as POI))
               return false;
            else
               poi.Add(ge as POI, 0);
            return true;
         }
         return false;
      }

      /// <summary>
      /// ändert Typ und Subtyp eines Elements
      /// </summary>
      /// <param name="ge"></param>
      /// <param name="typ"></param>
      /// <param name="subtyp"></param>
      /// <returns></returns>
      public bool ChangeTyp(GraphicElement ge, uint typ, uint subtyp) {
         if ((ge is Polygone && GetPolygone(typ, subtyp) != null) ||    // ex. schon
             (ge is Polyline && GetPolyline(typ, subtyp) != null) ||
             (ge is POI && GetPoi(typ, subtyp) != null))
            return false;
         Remove(ge);
         ge.Typ = typ;
         ge.Subtyp = subtyp;
         Insert(ge);
         return true;
      }


      #region nur zum Testen

      class NTTableItem {
         public UInt16 v1;
         public UInt16 v2;

         public NTTableItem(BinaryReaderWriter br) {
            v1 = br.ReadUInt16();
            v2 = br.ReadUInt16();
         }
         public override string ToString() {
            return string.Format("NTTableItem=[v1 0x{0:x}, v2 0x{1:x}]", v1, v2);
         }
      }

      protected void Test(BinaryReaderWriter br) {

         int txtcount = 0;
         uint offset = 0;
         string txt = "";
         while (offset < NT_PointLabelblock.Length) {
            txt = GetNTLabel(br, offset);
            offset = (uint)br.Position - NT_PointLabelblock.Offset;
            txtcount++;
         }
         Debug.WriteLine(string.Format("{0} Texte", txtcount));

         List<NTTableItem> block1 = new List<NTTableItem>();
         br.Seek(NT_LabelblockTable1.Offset);
         uint len = NT_LabelblockTable1.Length;
         while (len > 4) {
            block1.Add(new NTTableItem(br));
            len -= 4;
         }
         Debug.WriteLine(string.Format("{0} Einträge in nt2_block1", block1.Count));

         List<NTTableItem> block2 = new List<NTTableItem>();
         br.Seek(NT_LabelblockTable2.Offset);
         len = NT_LabelblockTable2.Length;
         while (len > 4) {
            block2.Add(new NTTableItem(br));
            len -= 4;
         }
         Debug.WriteLine(string.Format("{0} Einträge in nt2_block2", block2.Count));

         for (int i = 0; i < block1.Count; i++) {
            uint offs1 = block1[i].v1;
            uint key1 = block1[i].v2;
            string txt1 = GetNTLabel(br, offs1);

            //if (offs1 == 0xa552) {
            //   Debug.WriteLine(block1[i].ToString());
            //}

            bool found = false;
            for (int j = 0; j < block2.Count; j++) {
               uint key2 = block2[j].v1;
               if (key1 == key2) {
                  found = true;
                  uint offs2 = block2[j].v2;
                  string txt2 = GetNTLabel(br, offs2);
                  if (txt1 != txt2)
                     Debug.WriteLine(string.Format("{0}: {1} <--> {2} key 0x{3:x}", i, txt1, txt2, key1));
                  //else
                  //   Debug.WriteLine(string.Format("{0}: {1} OK", i, txt1));
                  break;
               }
            }
            if (!found)
               Debug.WriteLine(string.Format("{0}: {1} nicht gefunden", i, txt1));

         }



      }

      protected string GetNTLabel(BinaryReaderWriter br, uint offset) {
         br.Seek(NT_PointLabelblock.Offset + offset);
         return br.ReadString();
         //List<char> chars = new List<char>();
         //char c;
         //do {
         //   c = br.ReadChar();
         //   if (c != 0x0) chars.Add(c);
         //} while (c != 0x0);
         //return new string(chars.ToArray());
      }

      #endregion

   }

}
