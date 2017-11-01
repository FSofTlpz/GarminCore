/*
Copyright (C) 2011 Frank Stinner

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
using System.IO;
using System.Text;

namespace GarminCore.Files.Typ {

   /// <summary>
   /// Hilfsklasse um den Typ/Subtyp sowie den Offset zu den eigentlichen Daten in der Datei zu behandeln
   /// </summary>
   internal class TableItem {

      UInt16 typ;

      /// <summary>
      /// Typ (Bit 5...15, d.h. 0 .. 0x7ff)
      /// </summary>
      public uint Typ {
         get { return (uint)(typ >> 5); }
         set { typ = (UInt16)(Subtyp + ((value & 0x7ff) << 5)); }
      }
      /// <summary>
      /// Subtyp (Bit 0...4, d.h. 0 .. 0x1f)
      /// </summary>
      public uint Subtyp {
         get { return (uint)(typ & 0x1f); }
         set { typ = (UInt16)((typ & 0xffe0) + (value & 0x1f)); }
      }
      /// <summary>
      /// Offset zum Anfang des jeweiligen Blocks
      /// </summary>
      public int Offset { get; set; }

      public TableItem() {
         Typ = Subtyp = 0;
         Offset = 0;
      }

      public TableItem(BinaryReaderWriter br, int iItemlength)
         : this() {
         typ = br.ReadUInt16();
         switch (iItemlength) {
            case 3: Offset = br.ReadByte(); break;
            case 4: Offset = br.ReadUInt16(); break;
            case 5:
               Offset = br.ReadUInt16();
               Offset += br.ReadByte() << 16;    // falls das wirklich das höchstwertigste Byte ist
               break;
         }
      }

      public void Write(BinaryReaderWriter bw, int iItemlength) {
         UInt16 type = (UInt16)((Typ << 5) | Subtyp);
         bw.Write(type);
         switch (iItemlength) {
            case 3: bw.Write((byte)(Offset & 0xff)); break;
            case 4: bw.Write((UInt16)(Offset & 0xffff)); break;
            case 5:
               bw.Write((UInt16)(Offset & 0xffff));
               bw.Write((byte)((Offset >> 16) & 0xff));
               break;
         }
      }

      public override string ToString() {
         StringBuilder sb = new StringBuilder();
         sb.Append("DataItem=[");
         sb.Append("Typ=0x" + Typ.ToString("x"));
         sb.Append(" Subtyp=0x" + Subtyp.ToString("x"));
         sb.Append(" Offset=0x" + Offset.ToString("x"));
         sb.Append("]");
         return sb.ToString();
      }

   }

}
