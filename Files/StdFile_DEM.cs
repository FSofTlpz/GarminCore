﻿/*
Copyright (C) 2015 Frank Stinner

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
using System.IO;
using System.Text;
using GarminCore.Files.DEM;

namespace GarminCore.Files {

   /// <summary>
   /// zum Lesen (?) und Schreiben der DEM-Datei
   /// </summary>
   public class StdFile_DEM : StdFile {

      // http://wiki.openstreetmap.org/wiki/OSM_Map_On_Garmin/DEM_Subfile_Format

      #region Header-Daten

      /// <summary>
      /// Seems to be flags for interpreting the DEM data. First bit defines whether elevation is given in meter (0) or feet (1). On most maps any other bits are zero. (0x15)
      /// </summary>
      public UInt32 Flags { get; private set; }
      /// <summary>
      /// number of zoom levels (can be different from the number of map levels) (0x19)
      /// </summary>
      public UInt16 ZoomlevelCount { get; private set; }
      /// <summary>
      /// unbekannt, i.A. 0
      /// </summary>
      public byte[] Unknown_0x1B = { 0, 0, 0, 0 };
      /// <summary>
      /// Datensatzgröße für die Zoomlevel (immer 0x3C ?)
      /// </summary>
      public UInt16 ZoomlevelRecordSize { get; private set; }
      /// <summary>
      /// Pointer auf den 1 Zoomlevel-Datensatz
      /// </summary>
      public UInt32 PtrZoomlevel { get; private set; }
      /// <summary>
      /// unbekannt, i.A. 0 oder 1
      /// </summary>
      public byte[] Unknown_0x25 = { 0, 0, 0, 0 };

      #endregion

      /// <summary>
      /// Höhenangaben in Fuß oder Meter (Teil der <see cref="Flags"/>)
      /// </summary>
      public bool HeigthInFeet {
         get {
            return Bit.IsSet(Flags, 0);
         }
         set {
            Flags = Bit.Set(Flags, 0, value);
         }
      }


      /// <summary>
      /// Tabelleneintrag für die Zoomlevel-Tabelle
      /// </summary>
      //public class ZoomlevelTableitem {

      //   const ulong DEG_UNIT_FACTOR = 1UL << 32;

      //   /// <summary>
      //   /// spez. Type (i.A. 0, aber auch 1 gesehen)
      //   /// </summary>
      //   public byte SpecType { get; set; }
      //   /// <summary>
      //   /// Nummer des Eintrages (0, ...)
      //   /// </summary>
      //   public byte No { get; set; }
      //   /// <summary>
      //   /// Anzahl der Datenpunkte waagerecht
      //   /// </summary>
      //   public int PointsHoriz { get; set; }
      //   /// <summary>
      //   /// Anzahl der Datenpunkte senkrecht
      //   /// </summary>
      //   public int PointsVert { get; set; }
      //   /// <summary>
      //   /// Höhe -1 der letzten Zeile
      //   /// </summary>
      //   public int LastRowHeight { get; set; }
      //   /// <summary>
      //   /// Breite -1 der letzten Spalte
      //   /// </summary>
      //   public int LastColWidth { get; set; }
      //   /// <summary>
      //   /// unbekannt auf 0x12
      //   /// </summary>
      //   public short Unknown12 { get; set; }
      //   /// <summary>
      //   /// größter Subtile-Index waagerecht (Anzahl -1)
      //   /// </summary>
      //   public int MaxIdxHoriz { get; set; }
      //   /// <summary>
      //   /// größter Subtile-Index senkrecht (Anzahl -1)
      //   /// </summary>
      //   public int MaxIdxVert { get; set; }
      //   /// <summary>
      //   /// Struktur des Subtile-Tabelleneintrags (Länge der einzelnen Elemente)
      //   /// </summary>
      //   public short Structure { get; private set; }
      //   /// <summary>
      //   /// 1..3
      //   /// </summary>
      //   public int Structure_OffsetSize {
      //      get {
      //         return 1 + (Structure & 0x3);
      //      }
      //      set {
      //         switch (value) {
      //            case 1: Structure = (short)((Structure & 0xFFFC)); break;
      //            case 2: Structure = (short)((Structure & 0xFFFC) | 0x1); break;
      //            case 3: Structure = (short)((Structure & 0xFFFC) | 0x2); break;
      //            case 4: Structure = (short)((Structure & 0xFFFC) | 0x3); break;
      //         }
      //      }
      //   }
      //   /// <summary>
      //   /// 1, 2
      //   /// </summary>
      //   public int Structure_BaseheightSize {
      //      get {
      //         return 1 + ((Structure >> 2) & 0x1);
      //      }
      //      set {
      //         switch (value) {
      //            case 1: Structure = (short)((Structure & 0xFFFB)); break;
      //            case 2: Structure = (short)((Structure & 0xFFFB) | 0x4); break;
      //         }
      //      }
      //   }
      //   /// <summary>
      //   /// 1, 2
      //   /// </summary>
      //   public int Structure_DiffSize {
      //      get {
      //         return 1 + ((Structure >> 3) & 0x1);
      //      }
      //      set {
      //         switch (value) {
      //            case 1: Structure = (short)((Structure & 0xFFF7)); break;
      //            case 2: Structure = (short)((Structure & 0xFFF7) | 0x8); break;
      //         }
      //      }
      //   }
      //   /// <summary>
      //   /// 0, 1
      //   /// </summary>
      //   public int Structure_CodingtypeSize {
      //      get {
      //         return (Structure & 0x10) >> 4;
      //      }
      //      set {
      //         switch (value) {
      //            case 0: Structure = (short)((Structure & 0xFFEF)); break;
      //            case 1: Structure = (short)((Structure & 0xFFEF) | 0x10); break;
      //         }
      //      }
      //   }
      //   /// <summary>
      //   /// Länge des Subtile-Tabelleneintrags
      //   /// </summary>
      //   public short SubtileTableitemSize {
      //      get {
      //         return (short)(Structure_OffsetSize + Structure_BaseheightSize + Structure_DiffSize + Structure_CodingtypeSize);
      //      }
      //   }
      //   /// <summary>
      //   /// Pointer auf die Subtile-Tabelle (bezogen auf den Dateianfang)
      //   /// </summary>
      //   public uint PtrSubtileTable { get; set; }
      //   /// <summary>
      //   /// Pointer auf den Höhendatenbereich (bezogen auf den Dateianfang)
      //   /// </summary>
      //   public uint PtrHeightdata { get; set; }

      //   int _West = 0;
      //   /// <summary>
      //   /// westliche Grenze der Kachel
      //   /// </summary>
      //   public double West {
      //      get {
      //         return Unit2Degree(_West);
      //      }
      //      set {
      //         _West = Degree2Unit(value);
      //      }
      //   }

      //   int _North = 0;
      //   /// <summary>
      //   /// nördliche Grenze der Kachel
      //   /// </summary>
      //   public double North {
      //      get {
      //         return Unit2Degree(_North);
      //      }
      //      set {
      //         _North = Degree2Unit(value);
      //      }
      //   }

      //   int _PointDistanceHoriz = 0;
      //   /// <summary>
      //   /// waagerechter Abstand zwischen den Datenpunkten
      //   /// </summary>
      //   public double PointDistanceHoriz {
      //      get {
      //         return Unit2Degree(_PointDistanceHoriz);
      //      }
      //      set {
      //         _PointDistanceHoriz = Degree2Unit(value);
      //      }
      //   }

      //   int _PointDistanceVert = 0;
      //   /// <summary>
      //   /// senkrechter Abstand zwischen den Datenpunkten
      //   /// </summary>
      //   public double PointDistanceVert {
      //      get {
      //         return Unit2Degree(_PointDistanceVert);
      //      }
      //      set {
      //         _PointDistanceVert = Degree2Unit(value);
      //      }
      //   }

      //   /// <summary>
      //   /// kleinste Basishöhe eines Subtiles
      //   /// </summary>
      //   public short MinHeight { get; set; }
      //   /// <summary>
      //   /// größte Höhe
      //   /// </summary>
      //   public short MaxHeight { get; set; }

      //   public int SubtileCount {
      //      get {
      //         return (1 + MaxIdxHoriz) * (1 + MaxIdxVert);
      //      }
      //   }


      //   public ZoomlevelTableitem() {
      //      SpecType = 0;
      //      No = 0;
      //      PointsHoriz = PointsVert = 64;
      //      LastRowHeight = 64;
      //      LastColWidth = 64;
      //      Unknown12 = 0;
      //      MaxIdxHoriz = MaxIdxVert = 0;
      //      Structure = 0;
      //      Structure_OffsetSize = 3;
      //      Structure_BaseheightSize = 2;
      //      Structure_DiffSize = 2;
      //      Structure_CodingtypeSize = 1;
      //      West = 12.0;
      //      North = 54.0;
      //      PointDistanceHoriz = PointDistanceVert = 0.00028;
      //      MinHeight = 0;
      //      MaxHeight = 0;
      //   }

      //   public void Read(BinaryReaderWriter br, UInt16 recordlen) {
      //      if (recordlen >= 0x3C) {
      //         SpecType = br.ReadByte();
      //         No = br.ReadByte();
      //         PointsHoriz = br.ReadInt32();
      //         PointsVert = br.ReadInt32();
      //         LastRowHeight = br.ReadInt32();
      //         LastColWidth = br.ReadInt32();
      //         Unknown12 = br.ReadInt16();
      //         MaxIdxHoriz = br.ReadInt32();
      //         MaxIdxVert = br.ReadInt32();
      //         Structure = br.ReadInt16();
      //         short tmp = br.ReadInt16();   // SubtileTableitemSize: ergibt sich schon aus Structure
      //         PtrSubtileTable = br.ReadUInt32();
      //         PtrHeightdata = br.ReadUInt32();
      //         _West = br.ReadInt32();
      //         _North = br.ReadInt32();
      //         _PointDistanceVert = br.ReadInt32();
      //         _PointDistanceHoriz = br.ReadInt32();
      //         MinHeight = br.ReadInt16();
      //         MaxHeight = br.ReadInt16();
      //      }
      //   }

      //   public void Write(BinaryReaderWriter bw) {
      //      bw.Write(SpecType);
      //      bw.Write(No);
      //      bw.Write(PointsHoriz);
      //      bw.Write(PointsVert);
      //      bw.Write(LastRowHeight);
      //      bw.Write(LastColWidth);
      //      bw.Write(Unknown12);
      //      bw.Write(MaxIdxHoriz);
      //      bw.Write(MaxIdxVert);
      //      bw.Write(Structure);
      //      bw.Write(SubtileTableitemSize);
      //      bw.Write(PtrSubtileTable);
      //      bw.Write(PtrHeightdata);
      //      bw.Write(_West);
      //      bw.Write(_North);
      //      bw.Write(_PointDistanceVert);
      //      bw.Write(_PointDistanceHoriz);
      //      bw.Write(MinHeight);
      //      bw.Write(MaxHeight);
      //   }

      //   public static int Degree2Unit(double degree) {
      //      return (int)(degree / 360.0 * DEG_UNIT_FACTOR);
      //   }

      //   public static double Unit2Degree(int unit) {
      //      return unit * 360.0 / DEG_UNIT_FACTOR;
      //   }

      //}

      /// <summary>
      /// Tabelleneintrag für die Subtile-Tabelle
      /// </summary>
      //public class SubtileTableitem {
      //   /// <summary>
      //   /// Offset auf die Daten (bezogen auf den Anfang des Höhendatenbereichs)
      //   /// </summary>
      //   public uint Offset { get; set; }
      //   /// <summary>
      //   /// Bezugshöhe
      //   /// </summary>
      //   public short Baseheight { get; set; }
      //   /// <summary>
      //   /// max. Höhendiff.
      //   /// </summary>
      //   public ushort Diff { get; set; }
      //   /// <summary>
      //   /// Codiertyp
      //   /// </summary>
      //   public byte Type { get; set; }


      //   public SubtileTableitem(uint offset = 0, short baseheight = 0, ushort diff = 0, byte type = 0) {
      //      Offset = offset;
      //      Baseheight = baseheight;
      //      Diff = diff;
      //      Type = type;
      //   }

      //   /// <summary>
      //   /// liest einen Tabelleneintrag ein
      //   /// </summary>
      //   /// <param name="br"></param>
      //   /// <param name="offset_len">Länge des Speicherbereichs für den Offset in Byte</param>
      //   /// <param name="baseheight_len">Länge des Speicherbereichs für die Basishöhe in Byte</param>
      //   /// <param name="diff_len">Länge des Speicherbereichs in Byte</param>
      //   /// <param name="extraBytes">wenn größer 0, dann 1 zusätzliches Byte</param>
      //   public void Read(BinaryReaderWriter br, int offset_len = 3, int baseheight_len = 2, int diff_len = 2, int type_len = 1) {

      //      switch (offset_len) {
      //         case 1:
      //            Offset = br.ReadByte();
      //            break;

      //         case 2:
      //            Offset = br.ReadUInt16();
      //            break;

      //         case 3:
      //            Offset = br.Read3U();
      //            break;

      //         case 4:
      //            Offset = br.ReadUInt32();
      //            break;
      //      }

      //      switch (baseheight_len) {
      //         case 1:
      //            Baseheight = br.ReadByte();
      //            break;

      //         case 2:
      //            Baseheight = br.ReadInt16();
      //            break;
      //      }

      //      switch (diff_len) {
      //         case 1:
      //            Diff = br.ReadByte();
      //            break;

      //         case 2:
      //            Diff = br.ReadUInt16();
      //            break;
      //      }

      //      if (type_len > 0)
      //         Type = br.ReadByte();
      //   }

      //   /// <summary>
      //   /// schreibt den Tabelleneintrag
      //   /// </summary>
      //   /// <param name="bw"></param>
      //   /// <param name="offset_len">Byteanzahl für Offset</param>
      //   /// <param name="baseheight_len">Byteanzahl für Bezugshöhe</param>
      //   /// <param name="diff_len">Byteanzahl für Höhendiff</param>
      //   /// <param name="type_len">Byteanzahl für Codiertyp (hier auch 0 möglich)</param>
      //   public void Write(BinaryWriter w, int offset_len = 3, int baseheight_len = 2, int diff_len = 2, int type_len = 1) {
      //      // Offset
      //      switch (offset_len) {
      //         case 1:
      //            w.Write((byte)(Offset & 0xFF));
      //            break;
      //         case 2:
      //            w.Write((byte)(Offset & 0xFF));
      //            w.Write((byte)((Offset & 0xFF00) >> 8));
      //            break;
      //         case 3:
      //            w.Write((byte)(Offset & 0xFF));
      //            w.Write((byte)((Offset & 0xFF00) >> 8));
      //            w.Write((byte)((Offset & 0xFF0000) >> 16));
      //            break;
      //         case 4:
      //            w.Write((byte)(Offset & 0xFF));
      //            w.Write((byte)((Offset & 0xFF00) >> 8));
      //            w.Write((byte)((Offset & 0xFF0000) >> 16));
      //            w.Write((byte)((Offset & 0xFF000000) >> 24));
      //            break;
      //         default:
      //            throw new System.Exception("Die Offsetlänge im Tabelleneintrag darf größer als 4 sein.");
      //      }

      //      // Basishöhe
      //      switch (baseheight_len) {
      //         case 1:
      //            w.Write((byte)(Baseheight & 0xFF));
      //            break;
      //         case 2:
      //            w.Write((byte)(Baseheight & 0xFF));
      //            w.Write((byte)((Baseheight & 0xFF00) >> 8));
      //            break;
      //         default:
      //            throw new System.Exception("Die Basishöhenlänge im Tabelleneintrag darf größer als 2 sein.");
      //      }

      //      // Diff.
      //      switch (diff_len) {
      //         case 1:
      //            w.Write((byte)(Diff & 0xFF));
      //            break;
      //         case 2:
      //            w.Write((byte)(Diff & 0xFF));
      //            w.Write((byte)((Diff & 0xFF00) >> 8));
      //            break;
      //         default:
      //            throw new System.Exception("Die Differenzhöhenlänge im Tabelleneintrag darf größer als 2 sein.");
      //      }

      //      // Typ
      //      if (type_len > 0) {
      //         w.Write(Type);
      //      }

      //   }

      //   public override string ToString() {
      //      return string.Format("Offset 0x{0:X}, Baseheight 0x{1:X}, Diff 0x{2:X}, Type 0x{3:X}", Offset, Baseheight, Diff, Type);
      //   }

      //}

      /// <summary>
      /// Hier werden die Höhendaten und die Daten des Tabelleneintrages eines Subtiles zusammengefasst.
      /// </summary>
      public class Subtile {

         /// <summary>
         /// Tabelleneintrages des Subtiles
         /// </summary>
         public SubtileTableitem Tableitem { get; set; }
         /// <summary>
         /// codierte Höhendaten
         /// </summary>
         public byte[] Data { get; set; }
         /// <summary>
         /// Länge der Höhendaten
         /// </summary>
         public int DataLength {
            get {
               return Data.Length;
            }
         }


         public Subtile(byte[] data, SubtileTableitem tableitem = null) {
            SetData(data, tableitem);
         }

         public Subtile(string file, SubtileTableitem tableitem = null) {
            using (BinaryReader r = new BinaryReader(File.OpenRead(file))) {
               SetData(r.ReadBytes((int)r.BaseStream.Length), tableitem);
            }
         }

         void SetData(byte[] data, SubtileTableitem tableitem) {
            if (data == null || data.Length == 0)
               Data = new byte[0];
            else {
               Data = new byte[data.Length];
               data.CopyTo(Data, 0);
            }

            if (tableitem == null)
               Tableitem = new SubtileTableitem();
            else
               Tableitem = tableitem;
         }

         public override string ToString() {
            return string.Format("{0}, DataLength {1}", Tableitem, DataLength);
         }

      }


      public class Zoomlevel {

         public ZoomlevelTableitem ZoomlevelItem { get; set; }
         public List<Subtile> Subtiles { get; set; }

         public Zoomlevel() {
            ZoomlevelItem = new ZoomlevelTableitem();
            Subtiles = new List<Subtile>();
         }

         /// <summary>
         /// setzt die Offsets alle neu
         /// </summary>
         public void CalculateOffsets() {
            uint offs = 0;
            for (int i = 0; i < Subtiles.Count; i++) {
               Subtiles[i].Tableitem.Offset = offs;
               offs += (uint)Subtiles[i].DataLength;
            }
         }

         /// <summary>
         /// berechnet die notwendigen Speichergrößen
         /// </summary>
         public void CalculateStructureLength() {
            uint maxoffs = uint.MinValue;
            int maxbase = int.MinValue;
            uint maxdiff = uint.MinValue;
            for (int i = 0; i < Subtiles.Count; i++) {
               maxoffs = Math.Max(maxoffs, Subtiles[i].Tableitem.Offset);
               maxbase = Math.Max(maxbase, Subtiles[i].Tableitem.Baseheight);
               maxdiff = Math.Max(maxdiff, Subtiles[i].Tableitem.Diff);

            }

            if (maxoffs >= 65536)
               ZoomlevelItem.Structure_OffsetSize = 3;
            else if (maxoffs >= 256)
               ZoomlevelItem.Structure_OffsetSize = 2;
            else
               ZoomlevelItem.Structure_OffsetSize = 1;

            if (maxbase >= 256)
               ZoomlevelItem.Structure_BaseheightSize = 2;
            else
               ZoomlevelItem.Structure_BaseheightSize = 1;

            if (maxdiff >= 256)
               ZoomlevelItem.Structure_DiffSize = 2;
            else
               ZoomlevelItem.Structure_DiffSize = 1;
         }

         /// <summary>
         /// liefert die akt. Länge des Datenbereiches
         /// </summary>
         /// <returns></returns>
         public int GetDatalength() {
            int len = 0;
            for (int i = 0; i < Subtiles.Count; i++)
               len += Subtiles[i].DataLength;
            return len;
         }

         /// <summary>
         /// liefert die akt. Tabellengröße
         /// </summary>
         /// <returns></returns>
         public int GetTablelength() {
            return ZoomlevelItem.SubtileTableitemSize * Subtiles.Count;
         }

         /// <summary>
         ///  liest den Zoomlevel-Datensatz
         /// </summary>
         /// <param name="br"></param>
         /// <param name="recordlen">Satzlänge des Zoomlevels</param>
         public void Read(BinaryReaderWriter br, ushort recordlen) {
            ZoomlevelItem.Read(br, recordlen);
         }

         /// <summary>
         /// 
         /// </summary>
         /// <param name="br"></param>
         /// <param name="dataendpos">Pointer auf das 1. Byte NACH den Höhendaten</param>
         public void ReadData(BinaryReaderWriter br, uint dataendpos) {
            int items = (ZoomlevelItem.MaxIdxHoriz + 1) * (ZoomlevelItem.MaxIdxVert + 1);

            long pos = br.Position;
            br.Seek(ZoomlevelItem.PtrSubtileTable);
            List<SubtileTableitem> stilst = new List<SubtileTableitem>();
            for (int i = 0; i < items; i++) {
               stilst.Add(new SubtileTableitem());
               stilst[stilst.Count - 1].Read(br, ZoomlevelItem.Structure_OffsetSize, ZoomlevelItem.Structure_BaseheightSize, ZoomlevelItem.Structure_DiffSize, ZoomlevelItem.Structure_CodingtypeSize);
            }

            br.Seek(ZoomlevelItem.PtrHeightdata);
            for (int i = 0; i < stilst.Count; i++) {
               byte[] data = null;
               if (stilst[i].Diff > 0) {
                  // Datenlänge: vom akt. Offset bis zum nächsten gültigen Offset
                  int j = i + 1;
                  for (; j < stilst.Count; j++) {
                     if (stilst[j].Offset > 0)
                        break;
                  }

                  uint len = j < stilst.Count - 1 ?
                                    stilst[j].Offset - stilst[i].Offset :
                                    dataendpos - (uint)br.Position;
                  data = br.ReadBytes((int)len);
               }
               Subtiles.Add(new Subtile(data, stilst[i]));
            }

            br.Seek(pos);
         }

         /// <summary>
         /// schreibt die <see cref="SubtileTableitem"/> und die Höhendaten
         /// </summary>
         /// <param name="bw"></param>
         public void WriteData(BinaryReaderWriter bw) {
            CalculateOffsets();
            for (int i = 0; i < Subtiles.Count; i++)
               Subtiles[i].Tableitem.Write(bw, ZoomlevelItem.Structure_OffsetSize, ZoomlevelItem.Structure_BaseheightSize, ZoomlevelItem.Structure_DiffSize, ZoomlevelItem.Structure_CodingtypeSize);
            for (int i = 0; i < Subtiles.Count; i++)
               bw.Write(Subtiles[i].Data);
         }

         /// <summary>
         /// schreibt den Zoomlevel-Datensatz
         /// </summary>
         /// <param name="bw"></param>
         public void Write(BinaryReaderWriter bw) {
            ZoomlevelItem.Write(bw);
         }

         public override string ToString() {
            return ZoomlevelItem.ToString() + ", " + Subtiles.Count.ToString() + " Subtiles";
         }

      }


      /*
            /// <summary>
            /// Tabelle der Kachel-Infos und die Höhendaten
            /// </summary>
            public class Tiles4Zoomlevel {

               /// <summary>
               /// Daten eines Tiles (Offset, die eigentlichen Daten usw.)
               /// </summary>
               public class Tile {

                  #region Tabelleneintrag

                  /// <summary>
                  /// Offset in den Bereich der Höhendaten
                  /// </summary>
                  public UInt32 Block2Offset;
                  /// <summary>
                  /// Basishöhe
                  /// </summary>
                  public UInt16 BaseHeight;
                  /// <summary>
                  /// max. Höhendiff.
                  /// </summary>
                  public UInt16 Diff;
                  /// <summary>
                  /// gesehen: 0, ..., 6; 
                  /// </summary>
                  public byte ExtCodingInfo;

                  #endregion

                  /// <summary>
                  /// die eigentlichen Höhendaten
                  /// </summary>
                  public byte[] Data;

                  /// <summary>
                  /// Länge des Datenbereiches der Höhendaten
                  /// </summary>
                  public int DataLength {
                     get {
                        return Data != null ? Data.Length : 0;
                     }
                  }


                  public Tile() {
                     Block2Offset = 0;
                     BaseHeight = 0;
                     Diff = 0;
                     ExtCodingInfo = 0;
                     Data = null;
                  }

                  /// <summary>
                  /// liest die Metadaten ein
                  /// </summary>
                  /// <param name="br"></param>
                  /// <param name="start">Startpos. im Stream</param>
                  /// <param name="offsetlength">Länge des Speicherbereichs für den Offset in Byte</param>
                  /// <param name="baseheightlength">Länge des Speicherbereichs für die Basishöhe in Byte</param>
                  /// <param name="heightdifferencelength">Länge des Speicherbereichs in Byte</param>
                  /// <param name="extraBytes">wenn größer 0, dann 1 zusätzliches Byte</param>
                  public void Read(BinaryReaderWriter br, UInt32 start, int offsetlength, int baseheightlength, int heightdifferencelength, bool extraBytes) {
                     br.Seek(start);

                     switch (offsetlength) {
                        case 1:
                           Block2Offset = br.ReadByte();
                           break;

                        case 2:
                           Block2Offset = br.ReadUInt16();
                           break;

                        case 3:
                           Block2Offset = br.Read3U();
                           break;

                        case 4:
                           Block2Offset = br.ReadUInt32();
                           break;
                     }

                     switch (baseheightlength) {
                        case 1:
                           BaseHeight = br.ReadByte();
                           break;

                        case 2:
                           BaseHeight = br.ReadUInt16();
                           break;
                     }

                     switch (heightdifferencelength) {
                        case 1:
                           Diff = br.ReadByte();
                           break;

                        case 2:
                           Diff = br.ReadUInt16();
                           break;
                     }

                     if (extraBytes)
                        ExtCodingInfo = br.ReadByte();
                  }

                  /// <summary>
                  /// speichert die Metadaten
                  /// </summary>
                  /// <param name="wr"></param>
                  /// <param name="offsetlength">Länge des Speicherbereichs für den Offset in Byte</param>
                  /// <param name="baseheightlength">Länge des Speicherbereichs für die Basishöhe in Byte</param>
                  /// <param name="heightdifferencelength">Länge des Speicherbereichs in Byte</param>
                  /// <param name="extraBytes">wenn größer 0, dann 1 zusätzliches Byte</param>
                  public void Write(BinaryReaderWriter wr, int offsetlength, int baseheightlength, int heightdifferencelength, bool extraBytes) {
                     switch (offsetlength) {
                        case 1:
                           wr.Write((byte)Block2Offset);
                           break;

                        case 2:
                           wr.Write((UInt16)Block2Offset);
                           break;

                        case 3:
                           wr.Write3(Block2Offset);
                           break;

                        case 4:
                           wr.Write((UInt32)Block2Offset);
                           break;
                     }

                     switch (baseheightlength) {
                        case 1:
                           wr.Write((byte)BaseHeight);
                           break;

                        case 2:
                           wr.Write((UInt16)BaseHeight);
                           break;
                     }

                     switch (heightdifferencelength) {
                        case 1:
                           wr.Write((byte)Diff);
                           break;

                        case 2:
                           wr.Write((UInt16)Diff);
                           break;
                     }

                     if (extraBytes)
                        wr.Write(ExtCodingInfo);
                  }

                  /// <summary>
                  /// liest die eigentlichen Daten
                  /// </summary>
                  /// <param name="br"></param>
                  /// <param name="start"></param>
                  /// <param name="length"></param>
                  public void ReadData(BinaryReaderWriter br, uint start, uint length) {
                     if (length > 0) {
                        br.Seek(start);
                        Data = br.ReadBytes((int)length);
                     } else
                        Data = new byte[0];
                  }

                  /// <summary>
                  /// schreibt die eigentlichen Daten
                  /// </summary>
                  /// <param name="wr"></param>
                  public void WriteData(BinaryReaderWriter wr) {
                     if (Data != null && Data.Length > 0)
                        wr.Write(Data);
                  }

                  public override string ToString() {
                     return string.Format("Offset 0x{0:x}, BaseHeight={1}, Diff2Max={2}, Unknown=0x{3:x}, Datenlänge={4} Byte",
                                          Block2Offset, BaseHeight, Diff, ExtCodingInfo, DataLength);
                  }
               }

               /// <summary>
               /// Liste der Datensätze (Tiles)
               /// </summary>
               public List<Tile> Tiles3;


               public Tiles4Zoomlevel() {
                  Tiles3 = new List<Tile>();
               }

               /// <summary>
               /// liest den Block1 ein (nur Metadaten)
               /// </summary>
               /// <param name="br"></param>
               /// <param name="start"></param>
               /// <param name="recordlen"></param>
               /// <param name="records"></param>
               public void Read(BinaryReaderWriter br, UInt32 start, UInt32 startblock2, UInt16 recordlen,
                                int offsetlength, int baseheightlength, int heightdifferencelength, bool extraBytes) {
                  Tiles3.Clear();
                  int records = (int)((startblock2 - start) / recordlen);
                  for (int i = 0; i < records; i++) {
                     Tile r = new Tile();
                     r.Read(br, start + (UInt32)(i * recordlen), offsetlength, baseheightlength, heightdifferencelength, extraBytes);
                     Tiles3.Add(r);
                  }
               }

               public override string ToString() {
                  return string.Format("{0} Datensätze (Tiles)", Tiles3.Count);
               }
            }

            /// <summary>
            /// Tabelle der grundsätzlichen Infos über die Datenbereiche (Anzahl der Tiles, Pixel usw.)
            /// </summary>
            public class ZoomlevelTable {


               /// <summary>
               /// Liste der Datensätze (Zoomlevel)
               /// </summary>
               public List<ZoomlevelTableitem> Records;


               public ZoomlevelTable() {
                  Records = new List<ZoomlevelTableitem>();
               }

               /// <summary>
               /// liest den Block3 ein
               /// </summary>
               /// <param name="br"></param>
               /// <param name="start"></param>
               /// <param name="recordlen"></param>
               /// <param name="records"></param>
               public void Read(BinaryReaderWriter br, UInt32 start, UInt16 recordlen, UInt16 records) {
                  Records.Clear();
                  for (int i = 0; i < records; i++) {
                     ZoomlevelTableitem r = new ZoomlevelTableitem();
                     r.Read(br, start + (UInt32)(i * recordlen), recordlen);
                     Records.Add(r);
                  }
               }

               public void Write(BinaryReaderWriter bw) {
                  for (int i = 0; i < Records.Count; i++)
                     Records[i].Write(bw);
               }

               public override string ToString() {
                  return string.Format("{0} Datensätze (Zoomlevel)", Records.Count);
               }
            }

            public List<Tiles4Zoomlevel> Tiles2;
            public ZoomlevelTable Zoomleveltable;
      */

      public List<Zoomlevel> ZoomLevel;


      // Header
      // Block1a
      // Block2a
      // Block1b
      // Block2b
      // ...
      // Block3

      public StdFile_DEM()
         : base("DEM") {
         ZoomLevel = new List<Zoomlevel>();
      }

      public override void ReadHeader(BinaryReaderWriter br) {
         base.ReadCommonHeader(br, Typ);

         Flags = br.ReadUInt32();
         ZoomlevelCount = br.ReadUInt16();
         br.ReadBytes(Unknown_0x1B);
         ZoomlevelRecordSize = br.ReadUInt16();
         PtrZoomlevel = br.ReadUInt32();

         if (Headerlength >= 0x29) {
            br.ReadBytes(Unknown_0x25);

         }
      }

      protected override void ReadSections(BinaryReaderWriter br) { }

      protected override void DecodeSections() { }

      public override void Read(BinaryReaderWriter br, bool raw = false, uint headeroffset = 0, uint gapoffset = 0) {
         base.Read(br, raw, headeroffset, gapoffset);

         if (Locked != 0) {
            RawRead = true;
            return;
         }

         // Zoomlevel-Tabelle einlesen
         ZoomLevel = new List<Zoomlevel>();
         br.Seek(PtrZoomlevel);
         for (int i = 0; i < ZoomlevelCount; i++) {
            Zoomlevel zl = new Zoomlevel();
            zl.Read(br, ZoomlevelRecordSize);
            ZoomLevel.Add(zl);
         }

         // Subtile-Daten einlesen
         for (int i = 0; i < ZoomLevel.Count; i++) {
            Zoomlevel zl = ZoomLevel[i];
            uint end = PtrZoomlevel;
            if (i < ZoomLevel.Count - 1)
               end = ZoomLevel[i + 1].ZoomlevelItem.PtrSubtileTable;
            br.Seek(ZoomLevel[i].ZoomlevelItem.PtrSubtileTable);
            zl.ReadData(br, end);
         }

         if (raw) {  // keine Entschlüsselung der Höhendaten
            RawRead = true;
            return;
         }

         // Decodierung der Höhendaten


      }

      public override void Write(BinaryReaderWriter bw, uint headeroffset = 0, UInt16 headerlength = 0x29, uint gapoffset = 0, uint dataoffset = 0, bool setsectiondata = true) {
         HeaderOffset = headeroffset;
         if (headerlength > 0)
            Headerlength = headerlength;
         CreationDate = DateTime.Now;
         GapOffset = gapoffset;
         DataOffset = dataoffset;

         PtrZoomlevel = Headerlength;
         for (int z = 0; z < ZoomlevelCount; z++) {
            ZoomLevel[z].CalculateStructureLength();
            ZoomLevel[z].ZoomlevelItem.PtrSubtileTable = PtrZoomlevel;
            PtrZoomlevel += (uint)ZoomLevel[z].GetTablelength();
            ZoomLevel[z].ZoomlevelItem.PtrHeightdata = PtrZoomlevel;
            PtrZoomlevel += (uint)ZoomLevel[z].GetDatalength();
         }

         Encode_Header(bw);

         bw.Seek(headerlength);

         for (int z = 0; z < ZoomlevelCount; z++) {
            // Subtile-Tabelle schreiben
            for (int i = 0; i < ZoomLevel[z].Subtiles.Count; i++) {
               ZoomlevelTableitem zti = ZoomLevel[z].ZoomlevelItem;
               ZoomLevel[z].Subtiles[i].Tableitem.Write(bw, zti.Structure_OffsetSize, zti.Structure_BaseheightSize, zti.Structure_DiffSize, zti.Structure_CodingtypeSize);
            }
            // Subtile-Daten schreiben
            for (int i = 0; i < ZoomLevel[z].Subtiles.Count; i++)
               bw.Write(ZoomLevel[z].Subtiles[i].Data);
         }

         // Zoomleveltabelle schreiben
         for (int z = 0; z < ZoomlevelCount; z++)
            ZoomLevel[z].ZoomlevelItem.Write(bw);

         bw.Flush();
      }

      public override void Encode_Sections() { }
      protected override void Encode_Filesection(BinaryReaderWriter bw, int filesectiontype) { }
      public override void SetSectionsAlign() { }

      /// <summary>
      /// schreibt die Headerdaten und verwendet die akt. Dateiabschnitte dafür
      /// </summary>
      /// <param name="bw"></param>
      protected override void Encode_Header(BinaryReaderWriter bw) {
         if (bw != null) {
            base.Encode_Header(bw);

            ZoomlevelCount = (ushort)ZoomLevel.Count;
            ZoomlevelRecordSize = 0x3C;

            bw.Write(Flags);
            bw.Write(ZoomlevelCount);
            bw.Write(Unknown_0x1B);
            bw.Write(ZoomlevelRecordSize);
            bw.Write(PtrZoomlevel);
            bw.Write(Unknown_0x25);
         }
      }

      public override string ToString() {
         StringBuilder sb = new StringBuilder("Subtiles: ");
         for (int i = 0; i < ZoomLevel.Count; i++) {
            if (i == 0)
               sb.AppendFormat("{0}", ZoomLevel[i].Subtiles.Count);
            else
               sb.AppendFormat(", {0}", ZoomLevel[i].Subtiles.Count);
         }

         return base.ToString() +
                string.Format(", Datenbereiche: {0}, {1}",
                              ZoomLevel.Count,
                              sb.ToString());
      }

   }
}
