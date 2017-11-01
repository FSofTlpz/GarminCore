using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarminCore.Files {
   /// <summary>
   /// Infos über die Verbindungsknoten der Highways (z.Z. nur Behandlung von Rohdaten)
   /// </summary>
   public class StdFile_NOD : StdFile {

      #region Header-Daten

      /// <summary>
      /// NOD1-Section
      /// </summary>
      public DataBlock Nod1;

      /// <summary>
      /// Unknown. Lower bit always set. 0x01 and 0x27 spotted
      /// </summary>
      public byte Unknown_0x1D;

      /// <summary>
      /// Flag: 0x01: left-hand drive. others unknown (0x02 spotted)
      /// </summary>
      public byte Unknown_0x1E;

      /// <summary>
      /// Unknown. 0x0000
      /// </summary>
      public byte[] Unknown_0x1F = new byte[2];

      /// <summary>
      /// Unknown. 0x06
      /// </summary>
      public byte[] Unknown_0x21 = new byte[2];

      /// <summary>
      /// Unknown. 0x05
      /// </summary>
      public byte[] Unknown_0x23 = new byte[2];

      /// <summary>
      /// NOD2-Section
      /// </summary>
      public DataBlock Nod2;

      /// <summary>
      /// Unknown. 0x0000
      /// </summary>
      public byte[] Unknown_0x2D = new byte[4];

      /// <summary>
      /// NOD3-Section
      /// </summary>
      public DataBlockWithRecordsize Nod3;

      /// <summary>
      /// Unknown 0x00 and 0x0200 spotted
      /// </summary>
      public byte[] Unknown_0x3B = new byte[4];

      /// <summary>
      /// NOD4-Section
      /// </summary>
      public DataBlock Nod4;

      /// <summary>
      /// Unknown, 32 bytes 0x00
      /// </summary>
      public byte[] Unknown_0x47 = new byte[32];

      /// <summary>
      /// NOD5-Section
      /// </summary>
      public DataBlock Nod5;

      /// <summary>
      /// Unknown. 0x02 spotted
      /// </summary>
      public byte[] Unknown_0x6F = new byte[2];

      /// <summary>
      /// NOD6-Section
      /// </summary>
      public DataBlockWithRecordsize Nod6;

      /// <summary>
      /// Unknown 0x02 spotted 
      /// </summary>
      public byte[] Unknown_0x7B = new byte[4];

      #endregion

      enum InternalFileSections {
         PostHeaderData = 0,
         Nod1,
         Nod2,
         Nod3,
         Nod4,
         Nod5,
         Nod6,
      }



      public StdFile_NOD()
         : base("NOD") {
         Nod1 = new DataBlock();
         Unknown_0x1D = 0x1;
         Unknown_0x1E = 0x1;
         Unknown_0x21[0] = 6;
         Unknown_0x23[0] = 5;
         Nod2 = new DataBlock();
         Nod3 = new DataBlockWithRecordsize();
         Unknown_0x3B[0] = 2;
         Nod4 = new DataBlock();
         Nod5 = new DataBlock();
         Nod6 = new DataBlockWithRecordsize();
      }

      public override void ReadHeader(BinaryReaderWriter br) {
         base.ReadCommonHeader(br, Typ);

         Nod1.Read(br);
         Unknown_0x1D = br.ReadByte();
         Unknown_0x1E = br.ReadByte();
         br.ReadBytes(Unknown_0x1F);
         br.ReadBytes(Unknown_0x21);
         br.ReadBytes(Unknown_0x23);
         Nod2.Read(br);
         br.ReadBytes(Unknown_0x2D);
         Nod3.Read(br);
         br.ReadBytes(Unknown_0x3B);
         Nod4.Read(br);
         br.ReadBytes(Unknown_0x47);
         Nod5.Read(br);
         br.ReadBytes(Unknown_0x6F);
         Nod6.Read(br);
         br.ReadBytes(Unknown_0x7B);

      }

      protected override void ReadSections(BinaryReaderWriter br) {
         // --------- Dateiabschnitte für die Rohdaten bilden ---------
         Filesections.AddSection((int)InternalFileSections.Nod1, Nod1);
         Filesections.AddSection((int)InternalFileSections.Nod2, Nod2);
         Filesections.AddSection((int)InternalFileSections.Nod3, Nod3);
         Filesections.AddSection((int)InternalFileSections.Nod4, Nod4);
         Filesections.AddSection((int)InternalFileSections.Nod5, Nod5);
         Filesections.AddSection((int)InternalFileSections.Nod6, Nod6);
         if (GapOffset > HeaderOffset + Headerlength) // nur möglich, wenn extern z.B. auf den nächsten Header gesetzt
            Filesections.AddSection((int)InternalFileSections.PostHeaderData, HeaderOffset + Headerlength, GapOffset - (HeaderOffset + Headerlength));

         // Datenblöcke einlesen
         Filesections.ReadSections(br);

         SetSpecialOffsetsFromSections((int)InternalFileSections.PostHeaderData);
      }

      protected override void DecodeSections() {

         RawRead = true; // besser geht es noch nicht


         if (RawRead || Locked != 0) {
            RawRead = true;
            return;
         }


      }

      public override void Encode_Sections() {
         //SetData2Filesection((int)InternalFileSections.Nod1, true);

      }

      protected override void Encode_Filesection(BinaryReaderWriter bw, int filesectiontype) {
         switch ((InternalFileSections)filesectiontype) {
            case InternalFileSections.Nod1:
               //Encode_Nod1(bw);
               break;
         }
      }

      public override void SetSectionsAlign() {
         // durch Pseudo-Offsets die Reihenfolge der Abschnitte festlegen
         uint pos = 0;
         Filesections.SetOffset((int)InternalFileSections.PostHeaderData, pos++);
         Filesections.SetOffset((int)InternalFileSections.Nod1, pos++);
         Filesections.SetOffset((int)InternalFileSections.Nod2, pos++);
         Filesections.SetOffset((int)InternalFileSections.Nod3, pos++);
         Filesections.SetOffset((int)InternalFileSections.Nod4, pos++);
         Filesections.SetOffset((int)InternalFileSections.Nod5, pos++);
         Filesections.SetOffset((int)InternalFileSections.Nod6, pos++);

         Filesections.AdjustSections(DataOffset);     // lückenlos ausrichten

         Nod1 = new DataBlock(Filesections.GetPosition((int)InternalFileSections.Nod1));
         Nod2 = new DataBlock(Filesections.GetPosition((int)InternalFileSections.Nod2));
         Nod3 = new DataBlockWithRecordsize(Filesections.GetPosition((int)InternalFileSections.Nod3));
         Nod4 = new DataBlock(Filesections.GetPosition((int)InternalFileSections.Nod4));
         Nod5 = new DataBlock(Filesections.GetPosition((int)InternalFileSections.Nod5));
         Nod6 = new DataBlockWithRecordsize(Filesections.GetPosition((int)InternalFileSections.Nod6));
      }

      protected override void Encode_Header(BinaryReaderWriter bw) {
         if (bw != null) {
            base.Encode_Header(bw);

            // Header-Daten schreiben
            Nod1.Write(bw);
            bw.Write(Unknown_0x1D);
            bw.Write(Unknown_0x1E);
            bw.Write(Unknown_0x1F);
            bw.Write(Unknown_0x21);
            bw.Write(Unknown_0x23);
            Nod2.Write(bw);
            bw.Write(Unknown_0x2D);
            Nod3.Write(bw);
            bw.Write(Unknown_0x3B);
            Nod4.Write(bw);
            bw.Write(Unknown_0x47);
            Nod5.Write(bw);
            bw.Write(Unknown_0x6F);
            Nod6.Write(bw);
            bw.Write(Unknown_0x7B);

         }
      }


   }
}
