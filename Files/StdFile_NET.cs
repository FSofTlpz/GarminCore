/*
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

namespace GarminCore.Files {

   /// <summary>
   /// Infos über routable-Straßen
   /// </summary>
   public class StdFile_NET : StdFile {

      #region Header-Daten

      /// <summary>
      /// Road definitions (0x15)
      /// </summary>
      public DataBlock RoadDefinitionsBlock { get; private set; }

      /// <summary>
      /// Road definitions offset multiplier (power of 2) (0x1D)
      /// </summary>
      byte RoadDefinitionsOffsetMultiplier;

      /// <summary>
      /// Segmented roads (0x1E)
      /// </summary>
      public DataBlock SegmentedRoadsBlock { get; private set; }

      /// <summary>
      /// Segmented roads offset multiplier (power of 2) (0x26)
      /// </summary>
      byte SegmentedRoadsOffsetMultiplier;

      /// <summary>
      /// Sorted roads (0x27)
      /// </summary>
      public DataBlockWithRecordsize SortedRoadsBlock { get; private set; }

      public byte[] Unknown_0x31 = new byte[4];
      public byte Unknown_0x35;
      public byte Unknown_0x36;
      public byte[] Unknown_0x37 = new byte[4];
      public byte[] Unknown_0x3B = new byte[8];
      public DataBlock UnknownBlock_0x43 { get; private set; }
      public byte Unknown_0x4B;
      public DataBlock UnknownBlock_0x4C { get; private set; }
      public byte[] Unknown_0x54 = new byte[2];
      public DataBlock UnknownBlock_0x56 { get; private set; }
      public byte[] Unknown_0x5E = new byte[6];

      #endregion

      enum InternalFileSections {
         PostHeaderData = 0,
         RoadDefinitionsBlock,
         SegmentedRoadsBlock,
         SortedRoadsBlock,
         UnknownBlock_0x43,
         UnknownBlock_0x4C,
         UnknownBlock_0x56,
      }


      public class RoadData : BinaryReaderWriter.DataStruct {

         [Flags]
         public enum NodInfo : byte {
            unknown = 0x00,
            two_byte_pointer = 0x01,
            three_byte_pointer = 0x02,
         }

         [Flags]
         public enum RoadData2 : byte {
            unknown = 0x00,
            unknown0 = 0x01,
            oneway = 0x02,
            lock2road_shownextroad = 0x04,
            unknown3 = 0x08,
            has_street_address_info = 0x10,
            addr_start_right = 0x20,
            has_nod_info = 0x40,
            major_highway = 0x80
         }

         public RoadData() {
            LabelInfo = new List<uint>();
            SegmentedRoadOffsets = new List<uint>();
            Roaddata = RoadData2.unknown;
            RoadLength = 0;
            RgnIndexOverview = new List<byte>();
            Indexdata = new List<IndexData>();
            house_number_blocks = 0;
            street_address_flags = 0;
            NOD_info = NodInfo.unknown;
            NOD_Offset = 0;
         }

         /// <summary>
         /// Verweis in die Straßenliste einer Subdiv
         /// </summary>
         public class IndexData {
            public byte RoadIndex;
            public UInt16 Subdivisionnumber;

            public IndexData(byte roadIndex, UInt16 subdivisionnumber) {
               RoadIndex = roadIndex;
               Subdivisionnumber = subdivisionnumber;
            }

            public override string ToString() {
               return string.Format("Subdivisionnumber {0}, RoadIndex {1}", Subdivisionnumber, RoadIndex);
            }
         }


         /// <summary>
         /// bis zu 4 Labels je Straße möglich
         /// </summary>
         public List<uint> LabelInfo;
         public List<uint> SegmentedRoadOffsets;

         /// <summary>
         /// Art der vorhandenen Straßendaten
         /// </summary>
         public RoadData2 Roaddata;

         /// <summary>
         /// (halbe) Länge der Straße in Meter
         /// </summary>
         public uint RoadLength;

         /// <summary>
         /// Anzahl der <see cref="Indexdata"/>-Items je Level
         /// </summary>
         public List<byte> RgnIndexOverview;

         /// <summary>
         /// Liste aller Verweise in Straßenlisten von Subdivs
         /// </summary>
         public List<IndexData> Indexdata;

         /// <summary>
         /// immer 0 ?
         /// </summary>
         public byte house_number_blocks;

         /// <summary>
         /// gibt an welche Adressdaten vorhanden sind
         /// </summary>
         public byte street_address_flags;

         /// <summary>
         /// Index für die Zip-Tabelle
         /// </summary>
         public int ZipIndex;

         /// <summary>
         /// Index für die City-Tabelle
         /// </summary>
         public int CityIndex;

         /// <summary>
         /// Stream, der die Hausnummernbereiche enthält
         /// </summary>
         public byte[] NumberStream;

         public NodInfo NOD_info;

         public uint NOD_Offset;


         public override void Read(BinaryReaderWriter br, object data) {
            if (data == null)
               throw new Exception("RoadData.Read(): Die Daten können ohne gültige LBL-Datei nicht gelesen werden.");

            StdFile_LBL lbl = data as StdFile_LBL;
            uint tmpu = 0;
            bool label = true;
            do {
               tmpu = br.Read3U();
               if (label)
                  LabelInfo.Add(tmpu & 0x3FFFFF);
               else
                  SegmentedRoadOffsets.Add(tmpu & 0x3FFFFF);
               if ((tmpu & 0x400000) != 0)        // Bit 22: die nächstens Offsets sind SegmentedRoadOffsets
                  label = false;
            } while ((tmpu & 0x800000) == 0);     // Bit 23: Ende-Bit

            Roaddata = (RoadData2)br.ReadByte();

            RoadLength = br.Read3U();

            byte tmpb = 0;
            do {
               tmpb = br.ReadByte();
               RgnIndexOverview.Add((byte)(tmpb & 0x7F));
            } while ((tmpb & 0x80) == 0);    // Bit 7: Ende-Bit

            for (int i = 0; i < RgnIndexOverview.Count; i++)
               for (int j = 0; j < RgnIndexOverview[i]; j++)
                  Indexdata.Add(new IndexData(br.ReadByte(), br.ReadUInt16())); // road_index (in subdivision) und subdivision_number

            if ((Roaddata & RoadData2.has_street_address_info) != RoadData2.unknown) {
               house_number_blocks = br.ReadByte();         // immer 0 ?
               street_address_flags = br.ReadByte();

               // MKGMAP verwendet nur die Speicherung eines Index (1 oder 2 Byte), xxxxxx10
               ZipIndex = GetCityZip(br,
                                     (street_address_flags >> 2) & 0x03,
                                     lbl.ZipDataList.Count < 256 ? 1 : 2);
               // MKGMAP verwendet nur die Speicherung eines Index (1 oder 2 Byte), xxxx10xx
               CityIndex = GetCityZip(br,
                                      (street_address_flags >> 4) & 0x03,
                                      lbl.CityAndRegionOrCountryDataList.Count < 256 ? 1 : 2);
               // MKGMAP setzt 00xxxxxx für einen Nummern-Bitstream und
               //              01xxxxxx für einen Nummern-Bitstream der länger als 255 Byte ist
               // Je nach Länge des Bitstreams werden 1 oder 2 Byte für die Längenangabe verwendet.
               NumberStream = GetNumberStream(br, (street_address_flags >> 6) & 0x03);
            }

            if ((Roaddata & RoadData2.has_nod_info) != RoadData2.unknown) {
               NOD_info = (NodInfo)br.ReadByte();

               if ((NOD_info & NodInfo.two_byte_pointer) != NodInfo.unknown)
                  NOD_Offset = br.ReadUInt16();
               else
                  NOD_Offset = br.Read3U();
            }

         }

         public override void Write(BinaryReaderWriter bw, object data) {
            StdFile_LBL lbl = data as StdFile_LBL;
            uint tmpu = 0;
            for (int i = 0; i < LabelInfo.Count; i++) {
               tmpu = LabelInfo[i];
               if (i == LabelInfo.Count - 1) {
                  if (SegmentedRoadOffsets.Count > 0)
                     Bit.Set(tmpu, 14);
                  else
                     Bit.Set(tmpu, 15);
               }
            }
            for (int i = 0; i < SegmentedRoadOffsets.Count; i++) {
               tmpu = SegmentedRoadOffsets[i];
               if (i == SegmentedRoadOffsets.Count - 1)
                  Bit.Set(tmpu, 15);
            }
            bw.Write3(tmpu);

            bw.Write((byte)Roaddata);

            bw.Write3(RoadLength);

            for (int i = 0; i < RgnIndexOverview.Count; i++)
               if (i < RgnIndexOverview.Count - 1)
                  bw.Write(RgnIndexOverview[i]);
               else
                  bw.Write((byte)(RgnIndexOverview[i] | 0x80));

            for (int i = 0; i < Indexdata.Count; i++) {
               bw.Write(Indexdata[i].RoadIndex);
               bw.Write(Indexdata[i].Subdivisionnumber);
            }

            if ((Roaddata & RoadData2.has_street_address_info) != RoadData2.unknown) {
               bw.Write(house_number_blocks);
               bw.Write(street_address_flags);

               if (ZipIndex >= 0)
                  if (lbl.ZipDataList.Count < 256)
                     bw.Write((byte)ZipIndex);
                  else
                     bw.Write((UInt16)ZipIndex);

               if (CityIndex >= 0)
                  if (lbl.CityAndRegionOrCountryDataList.Count < 256)
                     bw.Write((byte)CityIndex);
                  else
                     bw.Write((UInt16)CityIndex);

               if (NumberStream != null && NumberStream.Length > 0) {
                  if (NumberStream.Length < 256)
                     bw.Write((byte)NumberStream.Length);
                  else
                     bw.Write((UInt16)NumberStream.Length);
                  bw.Write(NumberStream);
               }
            }

            if ((Roaddata & RoadData2.has_nod_info) != RoadData2.unknown) {
               bw.Write((byte)NOD_info);
               if ((NOD_info & NodInfo.two_byte_pointer) != NodInfo.unknown)
                  bw.Write((UInt16)NOD_Offset);
               else
                  bw.Write((byte)NOD_Offset);
            }

         }

         /// <summary>
         /// liefert den Index für die Zip- oder City-Tabelle (oder int.MinValue)
         /// </summary>
         /// <param name="br"></param>
         /// <param name="flag2bit"></param>
         /// <param name="bytes4idx"></param>
         /// <returns></returns>
         int GetCityZip(BinaryReaderWriter br, int flag2bit, int bytes4idx = 1) {
            int n;
            switch (flag2bit) {
               case 0x00:              // number field; Dekodierung unbekannt
                  n = br.ReadByte();
                  br.ReadBytes(n);
                  break;

               case 0x01:              // ?; Dekodierung unbekannt
                  //n = br.ReadByte();
                  //br.ReadBytes(n);
                  break;

               case 0x02:              // Index (LBL)
                  switch (bytes4idx) {
                     case 1:
                        return br.ReadByte();
                     case 2:
                        return br.ReadUInt16();
                     default:
                        throw new Exception("GetCityZip(): Dekodierung nicht bekannt.");
                  }

               case 0x03:              // keine Daten vorhanden
                  break;
            }
            return int.MinValue;
         }

         /// <summary>
         /// liefert den Stream, der die Hausnummernbereiche codiert (oder null)
         /// </summary>
         /// <param name="br"></param>
         /// <param name="flag2bit"></param>
         /// <returns></returns>
         byte[] GetNumberStream(BinaryReaderWriter br, int flag2bit) {
            int n;
            switch (flag2bit) {
               case 0x00:
                  n = br.ReadByte();
                  return br.ReadBytes(n);

               case 0x01:              // ?; Dekodierung unbekannt
                  n = br.ReadByte();
                  return br.ReadBytes(n);

               case 0x02:
                  n = br.ReadUInt16();
                  return br.ReadBytes(n);

               case 0x03:              // keine Daten vorhanden
                  break;
            }
            return null;
         }


         public override string ToString() {
            return string.Format("CityIndex {0}, ZipIndex {1}, RoadLength {2}, street_address_flags {3}",
                                 CityIndex,
                                 ZipIndex,
                                 RoadLength,
                                 street_address_flags);
         }

      }

      /// <summary>
      /// Liste der Straßendaten
      /// </summary>
      public List<RoadData> Roaddata;
      /// <summary>
      /// liefert den Index in <see cref="Roaddata"/> zum Offset aus <see cref="SortedOffsets"/>
      /// </summary>
      public SortedDictionary<uint, int> Idx4Offset;
      /// <summary>
      /// Offsets der Daten der alphabetisch sortierten Straßennamen
      /// </summary>
      public List<uint> SortedOffsets;

      /// <summary>
      /// muss vor dem Lesen/Schreiben gesetzt sein, wenn interpretierte Daten verwendet werden sollen
      /// <para>Andernfalls werden nur die Rohdaten der Datenblöcke verwendet.</para>
      /// </summary>
      public StdFile_LBL Lbl;


      public StdFile_NET()
         : base("NET") {
         Unknown_0x35 = 0x01;
         RoadDefinitionsOffsetMultiplier = 0;
         SegmentedRoadsOffsetMultiplier = 0;

         Roaddata = new List<RoadData>();
         Idx4Offset = new SortedDictionary<uint, int>();
         SortedOffsets = new List<uint>();
      }

      public override void ReadHeader(BinaryReaderWriter br) {
         base.ReadCommonHeader(br, Type);

         RoadDefinitionsBlock = new DataBlock(br);
         RoadDefinitionsOffsetMultiplier = br.ReadByte();
         SegmentedRoadsBlock = new DataBlock(br);
         SegmentedRoadsOffsetMultiplier = br.ReadByte();
         SortedRoadsBlock = new DataBlockWithRecordsize(br);
         br.ReadBytes(Unknown_0x31);
         Unknown_0x35 = br.ReadByte();
         Unknown_0x36 = br.ReadByte();

         // --------- Headerlänge > 55 Byte

         if (Headerlength >= 0x37) {
            br.ReadBytes(Unknown_0x37);

            if (Headerlength >= 0x3B) {
               br.ReadBytes(Unknown_0x3B);

               if (Headerlength >= 0x43) {
                  UnknownBlock_0x43 = new DataBlock(br);

                  if (Headerlength >= 0x4B) {
                     Unknown_0x4B = br.ReadByte();

                     if (Headerlength >= 0x4C) {
                        UnknownBlock_0x4C = new DataBlock(br);

                        if (Headerlength >= 0x54) {
                           br.ReadBytes(Unknown_0x54);

                           if (Headerlength >= 0x56) {
                              UnknownBlock_0x56 = new DataBlock(br);

                              if (Headerlength >= 0x5E) {
                                 br.ReadBytes(Unknown_0x5E);
                              }
                           }
                        }
                     }
                  }
               }
            }
         }
      }

      protected override void ReadSections(BinaryReaderWriter br) {
         // --------- Dateiabschnitte für die Rohdaten bilden ---------
         Filesections.AddSection((int)InternalFileSections.RoadDefinitionsBlock, new DataBlock(RoadDefinitionsBlock));
         Filesections.AddSection((int)InternalFileSections.SegmentedRoadsBlock, new DataBlock(SegmentedRoadsBlock));
         Filesections.AddSection((int)InternalFileSections.SortedRoadsBlock, new DataBlockWithRecordsize(SortedRoadsBlock));
         Filesections.AddSection((int)InternalFileSections.UnknownBlock_0x43, new DataBlock(UnknownBlock_0x43));
         Filesections.AddSection((int)InternalFileSections.UnknownBlock_0x4C, new DataBlock(UnknownBlock_0x4C));
         Filesections.AddSection((int)InternalFileSections.UnknownBlock_0x56, new DataBlock(UnknownBlock_0x56));
         if (GapOffset > HeaderOffset + Headerlength) // nur möglich, wenn extern z.B. auf den nächsten Header gesetzt
            Filesections.AddSection((int)InternalFileSections.PostHeaderData, HeaderOffset + Headerlength, GapOffset - (HeaderOffset + Headerlength));

         // Datenblöcke einlesen
         Filesections.ReadSections(br);

         SetSpecialOffsetsFromSections((int)InternalFileSections.PostHeaderData);
      }

      protected override void DecodeSections() {
         Roaddata.Clear();
         Idx4Offset.Clear();
         SortedOffsets.Clear();

         if (Locked != 0 || Lbl == null || Lbl.RawRead) {
            RawRead = true;
            return;
         }

         // Datenblöcke "interpretieren"
         int filesectiontype;

         filesectiontype = (int)InternalFileSections.RoadDefinitionsBlock;
         if (Filesections.GetLength(filesectiontype) > 0) {
            Decode_RoadDefinitionsBlock(Filesections.GetSectionDataReader(filesectiontype), new DataBlock(0, Filesections.GetLength(filesectiontype)), Lbl);
            Filesections.RemoveSection(filesectiontype);
         }

         filesectiontype = (int)InternalFileSections.SegmentedRoadsBlock;
         if (Filesections.GetLength(filesectiontype) > 0) {
            //Decode_SegmentedRoadsBlock(Filesections.GetSectionDataReader(filesectiontype), new DataBlock(0, Filesections.GetLength(filesectiontype)));
            //Filesections.RemoveSection(filesectiontype);
         }

         filesectiontype = (int)InternalFileSections.SortedRoadsBlock;
         if (Filesections.GetLength(filesectiontype) > 0) {
            DataBlockWithRecordsize bl = new DataBlockWithRecordsize(Filesections.GetPosition(filesectiontype));
            bl.Offset = 0;
            Decode_SortedRoadsBlock(Filesections.GetSectionDataReader(filesectiontype), bl);
            Filesections.RemoveSection(filesectiontype);
         }
      }

      public override void Encode_Sections() {
         SetData2Filesection((int)InternalFileSections.RoadDefinitionsBlock, true);
         SetData2Filesection((int)InternalFileSections.SegmentedRoadsBlock, true);
         SetData2Filesection((int)InternalFileSections.SortedRoadsBlock, true);
      }

      protected override void Encode_Filesection(BinaryReaderWriter bw, int filesectiontype) {
         switch ((InternalFileSections)filesectiontype) {
            case InternalFileSections.RoadDefinitionsBlock:
               if (Lbl == null)
                  return;
               Encode_RoadDefinitionsBlock(bw);
               break;

            case InternalFileSections.SegmentedRoadsBlock:
               Encode_SegmentedRoadsBlock(bw);
               break;

            case InternalFileSections.SortedRoadsBlock:
               Encode_SortedRoadsBlock(bw, Filesections.GetPosition(filesectiontype));
               break;

         }
      }

      public override void SetSectionsAlign() {
         // durch Pseudo-Offsets die Reihenfolge der Abschnitte festlegen
         uint pos = 0;
         Filesections.SetOffset((int)InternalFileSections.PostHeaderData, pos++);
         Filesections.SetOffset((int)InternalFileSections.RoadDefinitionsBlock, pos++);
         Filesections.SetOffset((int)InternalFileSections.SegmentedRoadsBlock, pos++);
         Filesections.SetOffset((int)InternalFileSections.SortedRoadsBlock, pos++);
         Filesections.SetOffset((int)InternalFileSections.UnknownBlock_0x43, pos++);
         Filesections.SetOffset((int)InternalFileSections.UnknownBlock_0x4C, pos++);
         Filesections.SetOffset((int)InternalFileSections.UnknownBlock_0x56, pos++);

         Filesections.AdjustSections(DataOffset);     // lückenlos ausrichten

         RoadDefinitionsBlock = new DataBlock(Filesections.GetPosition((int)InternalFileSections.RoadDefinitionsBlock));
         SegmentedRoadsBlock = new DataBlock(Filesections.GetPosition((int)InternalFileSections.SegmentedRoadsBlock));
         SortedRoadsBlock = new DataBlockWithRecordsize(Filesections.GetPosition((int)InternalFileSections.SortedRoadsBlock));
         UnknownBlock_0x43 = new DataBlock(Filesections.GetPosition((int)InternalFileSections.UnknownBlock_0x43));
         UnknownBlock_0x4C = new DataBlock(Filesections.GetPosition((int)InternalFileSections.UnknownBlock_0x4C));
         UnknownBlock_0x56 = new DataBlock(Filesections.GetPosition((int)InternalFileSections.UnknownBlock_0x56));
      }

      #region Encodierung der Datenblöcke

      void Encode_RoadDefinitionsBlock(BinaryReaderWriter bw) {
         if (bw != null) {
            for (int i = 0; i < Roaddata.Count; i++) {
               Roaddata[i].Write(bw, Lbl);
            }
         }
      }

      void Encode_SegmentedRoadsBlock(BinaryReaderWriter bw) {


         throw new Exception("Encode_SegmentedRoadsBlock() ist noch nicht implementiert.");


      }

      void Encode_SortedRoadsBlock(BinaryReaderWriter bw, DataBlockWithRecordsize src) {
         if (bw != null) {
            foreach (uint offs in SortedOffsets)
               switch (src.Recordsize) {
                  case 2: bw.Write((UInt16)offs); break;
                  case 3: bw.Write3(offs); break;
                  case 4: bw.Write(offs); break;
               }
         }
      }

      protected override void Encode_Header(BinaryReaderWriter bw) {
         if (bw != null) {
            base.Encode_Header(bw);

            RoadDefinitionsBlock.Write(bw);
            bw.Write(RoadDefinitionsOffsetMultiplier);
            SegmentedRoadsBlock.Write(bw);
            bw.Write(SegmentedRoadsOffsetMultiplier);
            RoadDefinitionsBlock.Write(bw);
            bw.Write(Unknown_0x31);
            bw.Write(Unknown_0x35);
            bw.Write(Unknown_0x36);

            if (Headerlength >= 0x37) {
               bw.Write(Unknown_0x37);

               if (Headerlength >= 0x3B)
                  bw.Write(Unknown_0x3B);

               if (Headerlength >= 0x43) {
                  UnknownBlock_0x43.Write(bw);

                  if (Headerlength >= 0x4B) {
                     bw.Write(Unknown_0x4B);

                     if (Headerlength >= 0x4C) {
                        UnknownBlock_0x4C.Write(bw);

                        if (Headerlength >= 0x54) {
                           bw.Write(Unknown_0x54);

                           if (Headerlength >= 0x56) {
                              UnknownBlock_0x56.Write(bw);

                              if (Headerlength >= 0x5E) {
                                 bw.Write(Unknown_0x5E);
                              }
                           }
                        }
                     }
                  }
               }
            }
         }
      }

      #endregion

      #region Decodierung der Datenblöcke

      void Decode_RoadDefinitionsBlock(BinaryReaderWriter br, DataBlock src, StdFile_LBL lbl) {
         uint end = src.Offset + src.Length;
         int idx = 0;
         while (br.Position < end) {
            Idx4Offset.Add((uint)br.Position - src.Offset, idx++);
            RoadData rd = new RoadData();
            rd.Read(br, lbl);
            Roaddata.Add(rd);
         }
      }

      void Decode_SegmentedRoadsBlock(BinaryReaderWriter br, DataBlock src) {


         throw new Exception("Decode_SegmentedRoadsBlock() ist noch nicht implementiert.");


      }

      void Decode_SortedRoadsBlock(BinaryReaderWriter br, DataBlockWithRecordsize src) {
         // eigentlich nur Bit 0..21, d.h. & 0x3FFFFF nötig
         // Bit 22-23: label_number (0-3)
         SortedOffsets = br.ReadUintArray(src);
         // roaddata[idx4offset[sortedoffsets[...]]]
      }

      #endregion

   }
}
