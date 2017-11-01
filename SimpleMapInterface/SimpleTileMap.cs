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
using GarminCore.DskImg;
using GarminCore.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace GarminCore.SimpleMapInterface {

   /// <summary>
   /// zum einfachen, stark eingeschränkten Einlesen, zu Verändern und Speichern einer einzelnen Karten-Kachel
   /// </summary>
   public class SimpleTileMap {

      /// <summary>
      /// Kartenbeschreibung
      /// </summary>
      public List<string> MapDescription { get; private set; }

      /// <summary>
      /// Copyrighttexte
      /// </summary>
      public List<string> Copyright { get; private set; }

      /// <summary>
      /// Datum der Erzeugung
      /// </summary>
      public DateTime CreationDate { get; set; }

      int _MapLayer;

      /// <summary>
      /// Kartenlayer (ein höherer Layer wird über einem niedrigeren gezeichnet; vermutlich nur bis 31 verwendbar?)
      /// </summary>
      public int MapLayer {
         get {
            return _MapLayer;
         }
         set {
            _MapLayer = value & 0xFFFFFF;
         }
      }

      /// <summary>
      /// Kartengrenzen
      /// </summary>
      public Bound MapBounds;

      /// <summary>
      /// Karten-ID (8stellige Zahl)
      /// </summary>
      public uint MapID;


      /// <summary>
      /// zur Verwaltung der symbolischen Maßstäbe und Bitanzahl
      /// </summary>
      public StdFile_TRE.SymbolicScaleDenominatorAndBits SymbolicScaleDenominatorAndBitsLevel { get; private set; }


      List<DetailMap> _SubdivMaps;

      /// <summary>
      /// Liste der Subdivmaps
      /// <para>Jede Subdiv-<see cref="DetailMap"/> kann selber eine Liste untergeordneter Subdivmaps haben. Dadurch wird eine Baumstruktur gebildet. 
      /// Für jeden Maßstab muß eine solche Ebene von Subdivmaps existieren, d.h. die Baumtiefe muss mit <see cref="ScaleAndBits4Levels"/> korrespondieren.</para>
      /// </summary>
      public List<DetailMap> SubdivMaps {
         get {
            return _SubdivMaps;
         }
         set {
            _SubdivMaps.Clear();
            MapBounds = null;
            if (value != null)
               while (value.Count > 0) {
                  DetailMap dm = value[0];
                  dm.ParentMap = null; // nur zur Sicherheit
                  value.RemoveAt(0);
                  _SubdivMaps.Add(dm);
                  if (MapBounds == null)
                     MapBounds = new Bound(dm.DesiredBounds);
                  else
                     MapBounds.Embed(dm.DesiredBounds);
               }

         }
      }

      /// <summary>
      /// liefert den Basisname der Garmin-Dateien (ohne Extension)
      /// </summary>
      public string BaseFilename { get; private set; }

      /// <summary>
      /// nur diese Punkttypen registrieren, falls ungleich null
      /// </summary>
      public SortedSet<int> ReadFilter4Pointtypes;

      /// <summary>
      /// nur diese Linientypen registrieren, falls ungleich null
      /// </summary>
      public SortedSet<int> ReadFilter4Linetypes;

      /// <summary>
      /// nur diese Flächentypen registrieren, falls ungleich null
      /// </summary>
      public SortedSet<int> ReadFilter4Areatypes;


      public SimpleTileMap() {
         MapDescription = new List<string>();
         Copyright = new List<string>();
         _SubdivMaps = new List<DetailMap>();
         MapLayer = 31;
         SymbolicScaleDenominatorAndBitsLevel = new StdFile_TRE.SymbolicScaleDenominatorAndBits();
         CreationDate = DateTime.Now;
      }

      /// <summary>
      /// Überprüfung, ob eine sinnvolle Datenstruktur vorliegt (sollte z.B. vor dem Speichern erfolgen)
      /// <para>Bei einem Fehler wird eine Exception ausgelöst.</para>
      /// </summary>
      public void CheckData() {
         for (int i = 0; i < SubdivMaps.Count; i++)
            SubdivMaps[i].CheckSubtree();

         int levels = SubdivMaps[0].Levels();
         for (int i = 1; i < SubdivMaps.Count; i++) {
            if (levels != SubdivMaps[0].Levels())
               throw new Exception("Unterschiedliche Ebenentiefe. (Teilbaum " + i.ToString() + ")");
            if (!MapBounds.IsEnclosed(SubdivMaps[0].DesiredBounds))
               throw new Exception("Eine Untergeordnete Karte liegt außerhalb des Gesamtbereiches. (Teilbaum " + i.ToString() + ")");
         }

         if (levels != SymbolicScaleDenominatorAndBitsLevel.Count)
            throw new Exception("Die Anzahl der Maßstab- und Bitdefinitionen stimmt nicht mit der Anzahl der Kartenebenen überein.");
      }

      /// <summary>
      /// löscht alle Daten aus dem Objekt
      /// </summary>
      public void Clear() {
         MapDescription.Clear();
         Copyright.Clear();
         SubdivMaps.Clear();
         SymbolicScaleDenominatorAndBitsLevel.Clear();
         MapBounds = null;
      }

      /// <summary>
      /// liefert eine Liste aller <see cref="DetailMap"/> einer bestimmten Ebene (0 ist die höchste)
      /// </summary>
      /// <param name="maplevel">Ebene 0..</param>
      /// <returns></returns>
      public List<DetailMap> GetSubdivmapsWithLevel(int maplevel) {
         if (0 <= maplevel && maplevel < SymbolicScaleDenominatorAndBitsLevel.Count)
            return GetSubdivmapsWithLevel(SubdivMaps, maplevel);
         return null;
      }

      /// <summary>
      /// liefert eine Liste aller <see cref="DetailMap"/> einer bestimmten Ebene (0 ist die höchste), die unterhalb der <see cref="parentmaps"/> sind
      /// </summary>
      /// <param name="parentmaps"></param>
      /// <param name="maplevel"></param>
      /// <returns></returns>
      static public List<DetailMap> GetSubdivmapsWithLevel(List<DetailMap> parentmaps, int maplevel) {
         List<DetailMap> maps = new List<DetailMap>();
         if (0 <= maplevel)
            foreach (DetailMap map in parentmaps)
               getSubdivmapsWithLevel(map, maplevel, maps);
         return maps;
      }

      /// <summary>
      /// Hilfsfkt. zur rekursiven Suche aller <see cref="DetailMap"/> einer bestimmten Ebene
      /// </summary>
      /// <param name="m"></param>
      /// <param name="maplevel"></param>
      /// <param name="maps"></param>
      static protected void getSubdivmapsWithLevel(DetailMap m, int maplevel, List<DetailMap> maps) {
         int parents = m.Parents();
         if (parents == maplevel)
            maps.Add(m);
         else
            if (parents < maplevel)
            for (int i = 0; i < m.ChildMapCount; i++)
               getSubdivmapsWithLevel(m.GetChildMap(i), maplevel, maps);
      }


      /// <summary>
      /// liefert alle verwendeten Linien-Typen der Ebene (0 ist die höchste)
      /// </summary>
      /// <param name="maplevel"></param>
      /// <returns></returns>
      public int[] GetAllLineTypes(int maplevel) {
         SortedSet<int> tmp = new SortedSet<int>();
         foreach (DetailMap map in GetSubdivmapsWithLevel(maplevel))
            foreach (DetailMap.Poly data in map.LineList)
               if (!tmp.Contains(data.Type))
                  tmp.Add(data.Type);
         int[] types = new int[tmp.Count];
         tmp.CopyTo(types);
         return types;
      }

      /// <summary>
      /// liefert alle verwendeten Flächen-Typen der Ebene (0 ist die höchste)
      /// </summary>
      /// <param name="maplevel"></param>
      /// <returns></returns>
      public int[] GetAllAreaTypes(int maplevel) {
         SortedSet<int> tmp = new SortedSet<int>();
         foreach (DetailMap map in GetSubdivmapsWithLevel(maplevel))
            foreach (DetailMap.Poly data in map.AreaList)
               if (!tmp.Contains(data.Type))
                  tmp.Add(data.Type);
         int[] types = new int[tmp.Count];
         tmp.CopyTo(types);
         return types;
      }

      /// <summary>
      /// liefert alle verwendeten POI-Typen der Ebene (0 ist die höchste)
      /// </summary>
      /// <param name="maplevel"></param>
      /// <returns></returns>
      public int[] GetAllPoiTypes(int maplevel) {
         SortedSet<int> tmp = new SortedSet<int>();
         foreach (DetailMap map in GetSubdivmapsWithLevel(maplevel))
            foreach (DetailMap.Point data in map.PointList)
               if (!tmp.Contains(data.Type))
                  tmp.Add(data.Type);
         int[] types = new int[tmp.Count];
         tmp.CopyTo(types);
         return types;
      }

#if DEBUG

      #region Funktionen zum Speichern der Karte als Bitmap (für Tests)

      class LonLat2Bitmap : IDisposable {

         double factor_lon;
         double factor_lat;

         Bitmap bm;

         public Bitmap Picture {
            get {
               Canvas.Flush();
               return bm;
            }
         }

         public Bound Area { get; private set; }

         public Graphics Canvas { get; private set; }


         public LonLat2Bitmap(Bound area, int bitmapwidth, int bitmapheight) {
            Area = new Bound(area);
            factor_lon = bitmapheight / area.Height;
            factor_lat = bitmapwidth / area.Width;
            bm = new Bitmap(bitmapwidth, bitmapheight);
            Canvas = Graphics.FromImage(bm);
            Canvas.Clear(Color.White);
         }

         public int Y(double lon) {
            return (int)((Area.Top - lon) * factor_lon);
         }

         public int X(double lat) {
            return (int)((lat - Area.Left) * factor_lat);
         }

         public void Save(string filename) {
            Picture.Save(filename + ".png", System.Drawing.Imaging.ImageFormat.Png);
         }

         #region Implemetierung der IDisposable-Schnittstelle

         /// <summary>
         /// true, wenn schon ein Dispose() erfolgte
         /// </summary>
         private bool _isdisposed = false;

         /// <summary>
         /// kann expliziet für das Objekt aufgerufen werden um interne Ressourcen frei zu geben
         /// </summary>
         public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
         }

         /// <summary>
         /// überschreibt die Standard-Methode
         /// <para></para>
         /// </summary>
         /// <param name="notfromfinalizer">falls, wenn intern vom Finalizer aufgerufen</param>
         protected virtual void Dispose(bool notfromfinalizer) {
            if (!_isdisposed) {            // bisher noch kein Dispose erfolgt
               if (notfromfinalizer) {          // nur dann alle managed Ressourcen freigeben
                  if (bm != null)
                     bm.Dispose();
               }
               // jetzt immer alle unmanaged Ressourcen freigeben (z.B. Win32)

               _isdisposed = true;        // Kennung setzen, dass Dispose erfolgt ist
            }
         }

         #endregion

      }

      public Bitmap Test_BuildBitmap(DetailMap map,
                                    int width, int height,
                                    IList<int> areatyps, IList<Color> areacolors,
                                    IList<int> linetyps, IList<Color> linecolors) {
         LonLat2Bitmap bm = new LonLat2Bitmap(map.DesiredBounds, width, height);

         foreach (var area in map.AreaList) {
            Color col = Color.Transparent;
            if (areatyps != null && areatyps.Count > 0) {
               for (int i = 0; i < areatyps.Count; i++)
                  if (area.Type == areatyps[i]) {
                     col = areacolors != null && i < areacolors.Count ? areacolors[i] : Color.LightGray;
                     break;
                  }
            } else
               col = Color.LightGray;

            if (col != Color.Transparent) {
               if (area.PointCount > 1) {
                  SolidBrush brush = new SolidBrush(col);
                  System.Drawing.Point[] p = new System.Drawing.Point[area.PointCount];
                  for (int i = 0; i < area.PointCount; i++) {
                     DetailMap.Poly.PolyPoint pt = area.GetPoint(i);
                     p[i].X = bm.X(pt.LongitudeDegree);
                     p[i].Y = bm.Y(pt.LatitudeDegree);
                  }
                  bm.Canvas.FillPolygon(brush, p);
               }
            }
         }

         foreach (var line in map.LineList) {
            Color col = Color.Transparent;
            if (linetyps != null && linetyps.Count > 0) {
               for (int i = 0; i < linetyps.Count; i++)
                  if (line.Type == linetyps[i]) {
                     col = linecolors != null && i < linecolors.Count ? linecolors[i] : Color.Black;
                     break;
                  }
            } else
               col = Color.Black;

            if (col != Color.Transparent) {
               if (line.PointCount > 1) {
                  Pen pen = new Pen(col, 0.1F);
                  System.Drawing.Point[] p = new System.Drawing.Point[line.PointCount];
                  for (int i = 0; i < line.PointCount; i++) {
                     DetailMap.Poly.PolyPoint pt = line.GetPoint(i);
                     p[i].X = bm.X(pt.LongitudeDegree);
                     p[i].Y = bm.Y(pt.LatitudeDegree);
                  }
                  bm.Canvas.DrawLines(pen, p);
               }
            }
         }

         return bm.Picture;
      }

      /// <summary>
      /// akt. Karte und ev. alle untergeordneten Karten als 1 Bild
      /// </summary>
      /// <param name="map"></param>
      /// <param name="filename"></param>
      /// <param name="rekursiv"></param>
      public void Test_SaveAsBitmap(DetailMap map, string filename, bool rekursiv = false) {
         //BuildTestBitmap(map, 1000, 1000, null, null, null, null).Save("../../tx.png", System.Drawing.Imaging.ImageFormat.Png);
         Test_BuildBitmap(map, 1000, 1000,
            new int[] {
               0x100,      // building
               0x500,      // parking
               0x3200,     // water
               0x5000,     // forest
               0x11000,    // commercial
               0x11010,    // wastewater
            },
            new Color[] {
               Color.DarkGray,
               Color.LightGray,
               Color.Blue,
               Color.LightGreen,
               Color.Orange,
               Color.Blue,
            },
            new int[] {
               0x300,      // primary
               0x600,      // minorstreet
               0x800,      // service
               0xa00,      // unclassified
               0x1800,     // stream
               0x1c00,
               0x2000,     // ele_minor
               0x2200,     // ele_major
               0x2900,     // powerline
               0x11309,    // footway2
               0x1130c,    // cyclefootway2
               0x11400,    // track1
               0x11401,    // track2
               0x11402,    // track3
               0x11405,    // path2
               0x11406,    // path3
               0x1140d,    // surface_bad
               0x11501,    // route_icn
               0x11504,    // route_lcn
               0x11505,    // route_xcn
            },
            new Color[] {
               Color.Red,
               Color.DarkGray,
               Color.DarkGray,
               Color.DarkGray,
               Color.Blue,
               Color.Gray,
               Color.SaddleBrown,
               Color.Brown,
               Color.Violet,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
               Color.RosyBrown,
            }
            ).Save(filename + ".png", System.Drawing.Imaging.ImageFormat.Png);

         if (rekursiv)
            for (int i = 0; i < map.ChildMapCount; i++)
               Test_SaveAsBitmap(map.GetChildMap(i), filename + "-" + i.ToString(), true);

         //for (int i = 0; i < tre.SubdivInfoList.Count; i++)
         //   if (dmlst[i] == map)
         //      Debug.WriteLine(i);
      }

      /// <summary>
      /// Karte als 1farbiges Bild
      /// </summary>
      /// <param name="map"></param>
      /// <param name="width"></param>
      /// <param name="height"></param>
      /// <param name="col"></param>
      /// <param name="filename"></param>
      public void Test_SaveAsMonochromBitmap(DetailMap map, int width, int height, Color col, string filename) {
         LonLat2Bitmap bm = new LonLat2Bitmap(MapBounds, width, height);

         int border_left = bm.X(map.DesiredBounds.Left);
         int border_right = bm.X(map.DesiredBounds.Right);
         int border_bottom = bm.Y(map.DesiredBounds.Bottom);
         int border_top = bm.Y(map.DesiredBounds.Top);

         Pen pen = new Pen(col, 2F);
         pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
         pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

         foreach (DetailMap.Poly line in map.AreaList) {
            if (line.PointCount > 1) {
               System.Drawing.Point[] p = new System.Drawing.Point[line.PointCount];
               for (int j = 0; j < line.PointCount; j++) {
                  DetailMap.Poly.PolyPoint pt = line.GetPoint(j);
                  p[j].X = bm.X(pt.LongitudeDegree);
                  p[j].Y = bm.Y(pt.LatitudeDegree);
                  //Debug.WriteLineIf(p[j].X < border_left || border_right < p[j].X || p[j].Y < border_top || border_bottom < p[j].Y,
                  //   string.Format("Fehler bei Gebiet {0}: Randüberschreitung {1} .. {2} .. {3} / {4} .. {5} .. {6}; {7} / {8}; {9}",
                  //                  j,
                  //                  border_left, p[j].X, border_right,
                  //                  border_bottom, p[j].Y, border_top,
                  //                  pt.Longitude, pt.Latitude,
                  //                  map.DesiredBounds));
               }
               bm.Canvas.DrawPolygon(pen, p);
            }
         }

         pen.Width = 0.1F;
         foreach (DetailMap.Poly line in map.LineList) {
            if (line.PointCount > 1) {
               System.Drawing.Point[] p = new System.Drawing.Point[line.PointCount];
               for (int j = 0; j < line.PointCount; j++) {
                  DetailMap.Poly.PolyPoint pt = line.GetPoint(j);
                  p[j].X = bm.X(pt.LongitudeDegree);
                  p[j].Y = bm.Y(pt.LatitudeDegree);
                  //Debug.WriteLineIf(p[j].X < border_left || border_right < p[j].X || p[j].Y < border_top || border_bottom < p[j].Y,
                  //   string.Format("Fehler bei Linie {0}: Randüberschreitung {1} .. {2} .. {3} / {4} .. {5} .. {6}; {7} / {8}; {9}",
                  //                  j,
                  //                  border_left, p[j].X, border_right,
                  //                  border_bottom, p[j].Y, border_top,
                  //                  pt.Longitude, pt.Latitude,
                  //                  map.DesiredBounds));
               }
               bm.Canvas.DrawLines(pen, p);
            }
         }

         pen.Width = 4F;
         if (map.DesiredBounds != null)
            bm.Canvas.DrawRectangle(pen,
                                    bm.X(map.DesiredBounds.Left),
                                    bm.Y(map.DesiredBounds.Top),
                                    bm.X(map.DesiredBounds.Right) - bm.X(map.DesiredBounds.Left),
                                    bm.Y(map.DesiredBounds.Bottom) - bm.Y(map.DesiredBounds.Top));

         bm.Save(string.IsNullOrEmpty(filename) ? "../../tst " + map.GetIndexPathAsString() : filename);
      }

      public Bitmap Test_SaveLevelAsBitmap0(int maplevel,
                                            int bitmapwidth, int bitmapheight,
                                            double left, double top, double width, double height,
                                            IList<int> areatyps, IList<Color> areacolors,
                                            IList<int> linetyps, IList<Color> linecolors,
                                            IList<int> poityps, IList<Color> poicolors) {
         LonLat2Bitmap bm = new LonLat2Bitmap(new Bound(left, left + width, top - height, top), bitmapwidth, bitmapheight);

         List<DetailMap> maps = GetSubdivmapsWithLevel(maplevel);

         foreach (DetailMap map in maps) {
            foreach (var area in map.AreaList) {
               Color col = Color.Transparent;
               if (areatyps != null && areatyps.Count > 0) {
                  for (int i = 0; i < areatyps.Count; i++)
                     if (area.Type == areatyps[i]) {
                        col = areacolors != null && i < areacolors.Count ? areacolors[i] : Color.LightGray;
                        break;
                     }
               } else
                  col = Color.LightGray;

               if (col != Color.Transparent) {
                  if (area.PointCount > 1) {
                     SolidBrush brush = new SolidBrush(col);
                     System.Drawing.Point[] p = new System.Drawing.Point[area.PointCount];
                     for (int i = 0; i < area.PointCount; i++) {
                        DetailMap.Poly.PolyPoint pt = area.GetPoint(i);
                        p[i].X = bm.X(pt.LongitudeDegree);
                        p[i].Y = bm.Y(pt.LatitudeDegree);
                     }
                     bm.Canvas.FillPolygon(brush, p);
                  }
               }
            }
         }

         foreach (DetailMap map in maps) {
            foreach (var line in map.LineList) {
               Color col = Color.Transparent;
               if (linetyps != null && linetyps.Count > 0) {
                  for (int i = 0; i < linetyps.Count; i++)
                     if (line.Type == linetyps[i]) {
                        col = linecolors != null && i < linecolors.Count ? linecolors[i] : Color.Black;
                        break;
                     }
               } else
                  col = Color.Black;

               if (col != Color.Transparent) {
                  if (line.PointCount > 1) {
                     Pen pen = new Pen(col, 0.1F);
                     System.Drawing.Point[] p = new System.Drawing.Point[line.PointCount];
                     for (int i = 0; i < line.PointCount; i++) {
                        DetailMap.Poly.PolyPoint pt = line.GetPoint(i);
                        p[i].X = bm.X(pt.LongitudeDegree);
                        p[i].Y = bm.Y(pt.LatitudeDegree);
                     }
                     bm.Canvas.DrawLines(pen, p);
                  }
               }
            }
         }

         return bm.Picture;
      }

      public void Test_SaveAsBitmap(List<DetailMap> maps, int width, int height, string filename) {
         List<Color> cols = new List<Color>();
         cols.Add(Color.Black);
         cols.Add(Color.Red);
         cols.Add(Color.Green);
         cols.Add(Color.Yellow);
         cols.Add(Color.Blue);
         cols.Add(Color.Orange);
         cols.Add(Color.Violet);
         cols.Add(Color.LightGray);
         cols.Add(Color.DarkGreen);
         cols.Add(Color.DarkMagenta);
         cols.Add(Color.DarkBlue);
         cols.Add(Color.DarkCyan);
         cols.Add(Color.DarkGray);
         cols.Add(Color.DarkRed);
         cols.Add(Color.DarkOrange);
         cols.Add(Color.DarkViolet);
         cols.Add(Color.LightBlue);
         cols.Add(Color.LightCyan);
         cols.Add(Color.LightGreen);
         cols.Add(Color.Magenta);
         Random r = new Random();
         while (maps != null && cols.Count < maps.Count)
            cols.Add(Color.FromArgb(r.Next(256), r.Next(256), r.Next(256)));

         LonLat2Bitmap bm = new LonLat2Bitmap(MapBounds, width, height);

         Pen pen = new Pen(Color.Black, 2F);
         pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
         pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

         for (int i = 0; i < maps.Count; i++) {
            DetailMap map = maps[i];
            pen.Color = cols[i];

            pen.Width = 0.1F;
            foreach (DetailMap.Point pt in map.PointList) {
               int x = bm.X(pt.Coordinates.LongitudeDegree);
               int y = bm.X(pt.Coordinates.LatitudeDegree);
               bm.Canvas.DrawLine(pen, x - 2, y - 2, x + 2, y + 2);
               bm.Canvas.DrawLine(pen, x + 2, y - 2, x - 2, y + 2);
            }

            pen.Width = 2F;
            foreach (DetailMap.Poly line in map.AreaList) {
               if (line.PointCount > 1) {
                  System.Drawing.Point[] p = new System.Drawing.Point[line.PointCount];
                  for (int j = 0; j < line.PointCount; j++) {
                     DetailMap.Poly.PolyPoint pt = line.GetPoint(j);
                     p[j].X = bm.X(pt.LongitudeDegree);
                     p[j].Y = bm.Y(pt.LatitudeDegree);
                  }
                  bm.Canvas.DrawPolygon(pen, p);
               }
            }

            pen.Width = 0.1F;
            foreach (DetailMap.Poly line in map.LineList) {
               if (line.PointCount > 1) {
                  System.Drawing.Point[] p = new System.Drawing.Point[line.PointCount];
                  for (int j = 0; j < line.PointCount; j++) {
                     DetailMap.Poly.PolyPoint pt = line.GetPoint(j);
                     p[j].X = bm.X(pt.LongitudeDegree);
                     p[j].Y = bm.Y(pt.LatitudeDegree);
                  }
                  bm.Canvas.DrawLines(pen, p);
               }
            }

            pen.Width = 4F;
            if (map.DesiredBounds != null)
               bm.Canvas.DrawRectangle(pen,
                                       bm.X(map.DesiredBounds.Left),
                                       bm.Y(map.DesiredBounds.Top),
                                       bm.X(map.DesiredBounds.Right) - bm.X(map.DesiredBounds.Left),
                                       bm.Y(map.DesiredBounds.Bottom) - bm.Y(map.DesiredBounds.Top));
         }

         bm.Save(filename);
      }

      public void Test_SaveLevelAsBitmap1(int width, int height, int maplevel, string filename) {
         Test_SaveAsBitmap(GetSubdivmapsWithLevel(maplevel), width, height, filename);
      }

      #endregion

#endif

      #region Daten aus einer vorhandenen Garmin-Karte einlesen

      /// <summary>
      /// liest die Daten aus den <see cref="BinaryReaderWriter"/>
      /// </summary>
      /// <param name="br_tre"></param>
      /// <param name="br_lbl"></param>
      /// <param name="br_rgn"></param>
      /// <param name="br_net"></param>
      /// <param name="maxlevel">max. Ebene bis zu der die Daten eingelesen werden (nur sinnvoll, falls die höchsten Auflösungen nicht benötigt werden)</param>
      public void Read(BinaryReaderWriter br_tre, BinaryReaderWriter br_lbl, BinaryReaderWriter br_rgn, BinaryReaderWriter br_net, int maxlevel = int.MaxValue) {
         StdFile_TRE tre = new StdFile_TRE();
         StdFile_LBL lbl = new StdFile_LBL();
         StdFile_RGN rgn = new StdFile_RGN(tre);
         StdFile_NET net = new StdFile_NET();

         tre.Read(br_tre);
         lbl.Read(br_lbl);
         rgn.Read(br_rgn);
         if (br_net != null)
            net.Read(br_net);

         GetFileData(tre, lbl, rgn, net, maxlevel);
      }

      /// <summary>
      /// liest die einzelnen Sub-Dateien mit dem angegebenen Basisnamen im angegebenen Pfad
      /// </summary>
      /// <param name="basefilenamepath"></param>
      /// <param name="maxlevel">max. Ebene bis zu der die Daten eingelesen werden (nur sinnvoll, falls die höchsten Auflösungen nicht benötigt werden)</param>
      public void Read(string basefilenamepath, int maxlevel = int.MaxValue) {
         string path = Path.GetDirectoryName(basefilenamepath);
         BaseFilename = Path.GetFileNameWithoutExtension(basefilenamepath);
         BinaryReaderWriter br_tre = null;
         BinaryReaderWriter br_lbl = null;
         BinaryReaderWriter br_rgn = null;
         BinaryReaderWriter br_net = null;

         string filename = Path.Combine(path, BaseFilename + ".tre");
         if (File.Exists(filename))
            br_tre = new BinaryReaderWriter(filename, true);

         filename = Path.Combine(path, BaseFilename + ".lbl");
         if (File.Exists(filename))
            br_lbl = new BinaryReaderWriter(filename, true);

         filename = Path.Combine(path, BaseFilename + ".rgn");
         if (File.Exists(filename))
            br_rgn = new BinaryReaderWriter(filename, true);

         filename = Path.Combine(path, BaseFilename + ".net");
         if (File.Exists(filename))
            br_net = new BinaryReaderWriter(filename, true);

         Read(br_tre, br_lbl, br_rgn, br_net, maxlevel);
      }

      /// <summary>
      /// liest die Daten aus den Sub-Dateien mit dem angegebenen Basisnamen aus dem <see cref="SimpleFilesystem"/>, also einer IMG-Datei
      /// </summary>
      /// <param name="fs"></param>
      /// <param name="basefilename"></param>
      /// <param name="maxlevel">max. Ebene bis zu der die Daten eingelesen werden (nur sinnvoll, falls die höchsten Auflösungen nicht benötigt werden)</param>
      public void Read(SimpleFilesystem fs, string basefilename, int maxlevel = int.MaxValue) {
         BaseFilename = basefilename;
         Read(fs, maxlevel);
      }

      /// <summary>
      /// liest die Daten mit dem angegebenen Basisnamen aus der IMG-Datei
      /// </summary>
      /// <param name="imgfile"></param>
      /// <param name="basefilename"></param>
      /// <param name="maxlevel">max. Ebene bis zu der die Daten eingelesen werden (nur sinnvoll, falls die höchsten Auflösungen nicht benötigt werden)</param>
      public void Read(string imgfile, string basefilename, int maxlevel = int.MaxValue) {
         using (BinaryReaderWriter br = new BinaryReaderWriter(File.Open(imgfile, FileMode.Open, FileAccess.Read, FileShare.Read))) {
            SimpleFilesystem fs = new SimpleFilesystem();
            fs.Read(br);
            Read(fs, basefilename, maxlevel);
         }
      }

      protected void Read(SimpleFilesystem fs, int maxlevel = int.MaxValue) {
         StdFile_TRE tre = new StdFile_TRE();
         StdFile_LBL lbl = new StdFile_LBL();
         StdFile_RGN rgn = new StdFile_RGN(tre);
         StdFile_NET net = new StdFile_NET();

         string filename = (BaseFilename + ".tre").ToUpper();
         BinaryReaderWriter br = fs.GetBinaryReaderWriter4File(filename);
         if (br != null)
            tre.Read(br);

         filename = (BaseFilename + ".lbl").ToUpper();
         br = fs.GetBinaryReaderWriter4File(filename);
         if (br != null)
            lbl.Read(br);

         filename = (BaseFilename + ".rgn").ToUpper();
         br = fs.GetBinaryReaderWriter4File(filename);
         if (br != null)
            rgn.Read(br);

         filename = (BaseFilename + ".net").ToUpper();
         br = fs.GetBinaryReaderWriter4File(filename);
         net.Lbl = lbl;
         if (br != null)
            net.Read(br);

         GetFileData(tre, lbl, rgn, net, maxlevel);
      }

      /// <summary>
      /// liest die Daten aus den Dateien ein
      /// </summary>
      /// <param name="tre"></param>
      /// <param name="lbl"></param>
      /// <param name="rgn"></param>
      /// <param name="net"></param>
      /// <param name="maxlevel">max. Ebene bis zu der die Daten eingelesen werden (nur sinnvoll, falls die höchsten Auflösungen nicht benötigt werden)</param>
      protected void GetFileData(StdFile_TRE tre, StdFile_LBL lbl, StdFile_RGN rgn, StdFile_NET net, int maxlevel = int.MaxValue) {
         Clear();

         for (int i = 0; i < tre.MapDescriptionList.Count; i++)
            MapDescription.Add(tre.MapDescriptionList[i]);

         for (int i = 0; i < tre.CopyrightOffsetsList.Count; i++)
            Copyright.Add(lbl.GetText(tre.CopyrightOffsetsList[i]));

         CreationDate = tre.CreationDate;

         MapLayer = tre.DisplayPriority;

         MapID = tre.MapID;

         MapBounds = new Bound(tre.West, tre.East, tre.South, tre.North);

         SymbolicScaleDenominatorAndBitsLevel = new StdFile_TRE.SymbolicScaleDenominatorAndBits(tre.SymbolicScaleDenominatorAndBitsLevel);

         // aus den Subdiv-Daten den Karten-Baum erzeugen
         List<DetailMap> subdivmaplst = new List<DetailMap>();
         for (int i = 0; i < rgn.SubdivList.Count && i < tre.SubdivInfoList.Count; i++) {
            int level = SymbolicScaleDenominatorAndBitsLevel.Level4SubdivIdx1(i);
            int coordbits = SymbolicScaleDenominatorAndBitsLevel.Bits(level);

            StdFile_RGN.SubdivData sd = rgn.SubdivList[i];
            StdFile_TRE.SubdivInfoBasic sdi = tre.SubdivInfoList[i];

            int halfheight = sdi.GetHalfHeightMapUnits(coordbits);
            int halfwidth = sdi.GetHalfWidthMapUnits(coordbits);
            Bound bound = new Bound(sdi.Center.Longitude - halfwidth,
                                    sdi.Center.Longitude + halfwidth,
                                    sdi.Center.Latitude - halfheight,
                                    sdi.Center.Latitude + halfheight);

            DetailMap dm = new DetailMap(null, bound);

            if (level <= maxlevel) {    // die höheren Auflösungen sollen nicht eingelesen werden

               // ================ Polygone verarbeiten

               //Debug.WriteLine(">>> PolygonList {0}", sd.PolygonList.Count);
               foreach (var item in sd.AreaList) {
                  if (ReadFilter4Areatypes == null || ReadFilter4Areatypes.Contains((item.Typ << 8) | (item.Subtyp))) {
                     DetailMap.Poly p = new DetailMap.Poly(item, sdi.Center, coordbits);
                     if (item.LabelOffset != 0)
                        if (!item.LabelInNET)            // das dürfte immer so sein
                           p.Label = lbl.GetText(item.LabelOffset);
                     dm.AreaList.Add(p);
                  }
               }

               //Debug.WriteLine(">>> ExtPolygonList {0}", sd.ExtPolygonList.Count);
               foreach (var item in sd.ExtAreaList) {
                  if (ReadFilter4Areatypes == null || ReadFilter4Areatypes.Contains((0x10000) | (item.Typ << 8) | (item.Subtyp))) {
                     DetailMap.Poly p = new DetailMap.Poly(item, sdi.Center, coordbits, true);
                     if (item.HasLabel)
                        p.Label = lbl.GetText(item.LabelOffset);
                     dm.AreaList.Add(p);
                  }
               }

               // ================ Linien verarbeiten

               //Debug.WriteLine(">>> PoylineList {0}", sd.PoylineList.Count);
               foreach (var item in sd.LineList) {
                  if (ReadFilter4Linetypes == null || ReadFilter4Linetypes.Contains((item.Typ << 8) | (item.Subtyp))) {
                     DetailMap.Poly p = new DetailMap.Poly(item, sdi.Center, coordbits);
                     if (item.LabelOffset != 0)
                        if (!item.LabelInNET)
                           p.Label = lbl.GetText(item.LabelOffset);
                        else
                           p.NetData = new DetailMap.RoadDataExt(net.Roaddata[net.Idx4Offset[item.LabelOffset]], lbl);
                     dm.LineList.Add(p);
                  }
               }

               //Debug.WriteLine(">>> ExtPolylineList {0}", sd.ExtPolylineList.Count);
               foreach (var item in sd.ExtLineList) {
                  if (ReadFilter4Linetypes == null || ReadFilter4Linetypes.Contains((0x10000) | (item.Typ << 8) | (item.Subtyp))) {
                     DetailMap.Poly p = new DetailMap.Poly(item, sdi.Center, coordbits, false);
                     if (item.HasLabel)
                        p.Label = lbl.GetText(item.LabelOffset);
                     dm.LineList.Add(p);
                  }
               }

               // ================ Punkte verarbeiten

               foreach (var item in sd.IdxPointList) {      // vor den "normalen" Punkten einlesen, damit der ev. Index-Verweise stimmen (z.B. für Exits)
                  if (ReadFilter4Pointtypes == null || ReadFilter4Pointtypes.Contains((item.Typ << 8) | (item.Subtyp))) {
                     DetailMap.Point p = new DetailMap.Point(item, sdi.Center, coordbits);

                     if (item.LabelOffset != 0)
                        if (!item.IsPoiOffset)
                           p.Label = lbl.GetText(item.LabelOffset);
                        else {
                           int idx = lbl.POIPropertiesListOffsets[item.LabelOffset];
                           DetailMap.PoiDataExt pd = new DetailMap.PoiDataExt(lbl.POIPropertiesList[idx], lbl);
                           p.LblData = pd;
                           p.Label = p.LblData.Text;
                        }

                     dm.PointList.Add(p);
                  }
               }

               foreach (var item in sd.PointList) {
                  if (ReadFilter4Pointtypes == null || ReadFilter4Pointtypes.Contains((item.Typ << 8) | (item.Subtyp))) {
                     DetailMap.Point p = new DetailMap.Point(item, sdi.Center, coordbits);

                     if (item.LabelOffset != 0)
                        if (!item.IsPoiOffset)
                           p.Label = lbl.GetText(item.LabelOffset);
                        else {
                           if (lbl.POIPropertiesListOffsets.ContainsKey(item.LabelOffset)) {
                              int idx = lbl.POIPropertiesListOffsets[item.LabelOffset];
                              DetailMap.PoiDataExt pd = new DetailMap.PoiDataExt(lbl.POIPropertiesList[idx], lbl);
                              p.LblData = pd;
                              p.Label = p.LblData.Text;
                           } else
                              Debug.WriteLine("Fehler bei IsPoiOffset=" + item.LabelOffset.ToString() + ", aber ohne gültige POIPropertiesListOffsets?");
                        }

                     dm.PointList.Add(p);
                  }
               }

               foreach (var item in sd.ExtPointList) {
                  if (ReadFilter4Pointtypes == null || ReadFilter4Pointtypes.Contains((0x10000) | (item.Typ << 8) | (item.Subtyp))) {
                     DetailMap.Point p = new DetailMap.Point(item, sdi.Center, coordbits);

                     if (item.HasLabel)
                        p.Label = lbl.GetText(item.LabelOffset);

                     if (item.HasExtraBytes)
                        p.GarminExtraData = item.ExtraBytes;

                     dm.PointList.Add(p);
                  }
               }
            }

            subdivmaplst.Add(dm);
         }

         for (int i = 0; i < subdivmaplst.Count; i++) {
            if (subdivmaplst[i].ParentMap == null)
               linkChilds(i, tre.SubdivInfoList, subdivmaplst);
            if (subdivmaplst[i].ParentMap == null)
               SubdivMaps.Add(subdivmaplst[i]);
         }

         //SaveAsBitmap(ChildMaps[0].ChildMaps[3].ChildMaps[0], "../../dm-0-3-0", true);
      }

      /// <summary>
      /// verlinkt die Liste der eingelesenen Subdiv-<see cref="DetailMap"/>'s zu einer Baumstruktur
      /// </summary>
      /// <param name="parentidx"></param>
      /// <param name="sdi"></param>
      /// <param name="subdivlst"></param>
      void linkChilds(int parentidx, List<StdFile_TRE.SubdivInfoBasic> sdi, List<DetailMap> subdivlst) {
         if (sdi[parentidx] is StdFile_TRE.SubdivInfo) {
            int first = (sdi[parentidx] as StdFile_TRE.SubdivInfo).FirstChildSubdivIdx1 - 1;
            int last = first + (sdi[parentidx] as StdFile_TRE.SubdivInfo).ChildSubdivInfos - 1;
            for (int i = first; i <= last && i < subdivlst.Count; i++) { // es wurde eine Karte gefunden, die in der TRE-Datei 1 SubdivInfo mehr als Subdivs in der RGN-Datei enthielt
               subdivlst[i].ParentMap = subdivlst[parentidx];
               linkChilds(i, sdi, subdivlst);
            }
         }
      }

      #endregion

      #region Daten als eine (sehr einfache) Garmin-Karte speichern (nur TRE, LBL und RGN)

      /// <summary>
      /// schreibt die Daten in ein <see cref="SimpleFilesystem"/> mit dem aktuellen <see cref="BaseFilename"/>
      /// </summary>
      /// <param name="fs"></param>
      /// <param name="withpoiidx"></param>
      protected void Write(SimpleFilesystem fs, bool withpoiidx) {
         if (string.IsNullOrEmpty(BaseFilename) ||
             BaseFilename.Length != 8)
            throw new Exception("Es existiert kein gültiger Basisdateiname.");

         StdFile_TRE tre = new StdFile_TRE();
         StdFile_LBL lbl = new StdFile_LBL();
         StdFile_RGN rgn = new StdFile_RGN(tre);

         SetFileData(tre, lbl, rgn, withpoiidx);

         MemoryStream mem;

         mem = new MemoryStream();
         using (BinaryReaderWriter bw = new BinaryReaderWriter(mem)) {
            rgn.Write(bw);
            string filename = BaseFilename + ".RGN";
            if (fs.FilenameExist(filename))
               fs.FileDelete(filename);
            fs.FileAdd(filename, (uint)mem.Length);
            using (BinaryReaderWriter bwf = fs.GetBinaryReaderWriter4File(filename))
               bwf.Write(mem.ToArray());
         }

         mem = new MemoryStream();
         using (BinaryReaderWriter bw = new BinaryReaderWriter(mem)) {
            tre.Write(bw);
            string filename = BaseFilename + ".TRE";
            if (fs.FilenameExist(filename))
               fs.FileDelete(filename);
            fs.FileAdd(filename, (uint)mem.Length);
            using (BinaryReaderWriter bwf = fs.GetBinaryReaderWriter4File(filename))
               bwf.Write(mem.ToArray());
         }

         mem = new MemoryStream();
         using (BinaryReaderWriter bw = new BinaryReaderWriter(mem)) {
            lbl.Write(bw);
            string filename = BaseFilename + ".LBL";
            if (fs.FilenameExist(filename))
               fs.FileDelete(filename);
            fs.FileAdd(filename, (uint)mem.Length);
            using (BinaryReaderWriter bwf = fs.GetBinaryReaderWriter4File(filename))
               bwf.Write(mem.ToArray());
         }

      }

      /// <summary>
      /// schreibt die Daten in ein <see cref="SimpleFilesystem"/> mit dem neuen BaseFilename
      /// </summary>
      /// <param name="fs"></param>
      /// <param name="basefilename"></param>
      /// <param name="withpoiidx"></param>
      public void Write(SimpleFilesystem fs, string basefilename, bool withpoiidx) {
         BaseFilename = basefilename;
         Write(fs, withpoiidx);
      }

      /// <summary>
      /// schreibt die Daten in eine Kachel-IMG-Datei mit dem neuen BaseFilename
      /// </summary>
      /// <param name="imgfile"></param>
      /// <param name="basefilename"></param>
      /// <param name="withpoiidx"></param>
      public void Write(string imgfile, string basefilename, bool withpoiidx) {
         SimpleFilesystem fs = new SimpleFilesystem();
         fs.ImgHeader.FileBlockLength = 0x200;
         fs.ImgHeader.FATBlockLength = 0x200;
         fs.ImgHeader.HeadSectors = 2;

         Write(fs, basefilename, withpoiidx);
         using (BinaryReaderWriter bw = new BinaryReaderWriter(File.Open(imgfile, FileMode.Create, FileAccess.Write, FileShare.None)))
            fs.Write(bw);
      }

      /// <summary>
      /// schreibt die Daten in die entsprechenden <see cref="BinaryReaderWriter"/>
      /// </summary>
      /// <param name="bw_tre"></param>
      /// <param name="bw_lbl"></param>
      /// <param name="bw_rgn"></param>
      /// <param name="withpoiidx"></param>
      public void Write(BinaryReaderWriter bw_tre, BinaryReaderWriter bw_lbl, BinaryReaderWriter bw_rgn, bool withpoiidx) {
         StdFile_TRE tre = new StdFile_TRE();
         StdFile_LBL lbl = new StdFile_LBL();
         StdFile_RGN rgn = new StdFile_RGN(tre);

         SetFileData(tre, lbl, rgn, withpoiidx);

         tre.Write(bw_tre);
         lbl.Write(bw_lbl);
         rgn.Write(bw_rgn);
         bw_tre.Dispose();
         bw_lbl.Dispose();
         bw_rgn.Dispose();
      }

      /// <summary>
      /// schreibt die einzelnen Dateien in den gewünschten Pfad mit dem neuen BaseFilename
      /// </summary>
      /// <param name="basefilenamewithpath"></param>
      /// <param name="withpoiidx"></param>
      public void Write(string basefilenamewithpath, bool withpoiidx) {
         BaseFilename = Path.GetFileNameWithoutExtension(basefilenamewithpath);
         Write(new BinaryReaderWriter(File.Open(basefilenamewithpath + ".tre", FileMode.Create, FileAccess.Write, FileShare.None)),
               new BinaryReaderWriter(File.Open(basefilenamewithpath + ".lbl", FileMode.Create, FileAccess.Write, FileShare.None)),
               new BinaryReaderWriter(File.Open(basefilenamewithpath + ".rgn", FileMode.Create, FileAccess.Write, FileShare.None)),
               withpoiidx);
      }

      /// <summary>
      /// Hilfsfunktionen zum Erzeugen der LBL-Daten
      /// </summary>
      class HelperLbl {

         /// <summary>
         /// zum (sortierten) Sammeln von Texten
         /// </summary>
         class SimpleTextBag {

            SortedSet<string> text;
            List<string> table;

            /// <summary>
            /// Zeigt an, ob der <see cref="SimpleTextBag"/> schon abgeschlossen ist.
            /// </summary>
            public bool IsClosed { get; private set; }

            public int Count {
               get {
                  return IsClosed ? table.Count : text.Count;
               }
            }


            public SimpleTextBag() {
               text = new SortedSet<string>();
               IsClosed = false;
            }

            /// <summary>
            /// fügt einen Text in den <see cref="SimpleTextBag"/> ein
            /// <para>Ist der <see cref="SimpleTextBag"/> schon abgeschlossen, wird eine Exception ausgelöst.</para>
            /// </summary>
            /// <param name="txt"></param>
            public void Add(string txt) {
               if (IsClosed)
                  throw new Exception("Der SimpleTextBag ist schon abgeschlossen.");
               if (!text.Contains(txt))
                  text.Add(txt);
            }

            /// <summary>
            /// schließt den <see cref="SimpleTextBag"/> ab
            /// </summary>
            public void Close() {
               IsClosed = true;
               table = new List<string>();
               foreach (string txt in text)     // sortiert einfügen
                  table.Add(txt);
               text.Clear();
            }

            /// <summary>
            /// liefert den Index eines Textes
            /// <para>Ist der <see cref="SimpleTextBag"/> noch nicht abgeschlossen, wird eine Exception ausgelöst.</para>
            /// </summary>
            /// <param name="txt"></param>
            /// <returns></returns>
            public int Index(string txt) {
               if (!IsClosed)
                  throw new Exception("Der SimpleTextBag ist noch nicht abgeschlossen.");
               return table.BinarySearch(txt);
            }

            public string Text(int idx) {
               return table[idx];
            }

            /// <summary>
            /// liefert die Tabelle aller Texte
            /// <para>Ist der <see cref="SimpleTextBag"/> noch nicht abgeschlossen, wird eine Exception ausgelöst.</para>
            /// </summary>
            /// <returns></returns>
            public string[] GetTable() {
               if (!IsClosed)
                  throw new Exception("Der SimpleTextBag ist noch nicht abgeschlossen.");
               string[] tmp = new string[table.Count];
               int idx = 0;
               foreach (var item in table)
                  tmp[idx++] = item;
               return tmp;
            }

         }

         /// <summary>
         /// zum (sortierten) Sammeln von erweiterten Texten
         /// </summary>
         class SimpleTextBagExt {

            /// <summary>
            /// Das Item besteht aus dem Haupttext, ev. einem Flag und einem zusätzlichen Text.
            /// </summary>
            class DataItem : IComparable {
               public string text1;
               public string text2;
               public bool flag;

               public DataItem(string text1, bool flag, string text2) {
                  this.text1 = string.IsNullOrEmpty(text1) ? "" : text1;
                  this.text2 = string.IsNullOrEmpty(text2) ? "" : text2;
                  this.flag = flag;
               }

               public int CompareTo(object obj) {
                  DataItem di2 = obj as DataItem;

                  int cmp = string.Compare(text1, di2.text1);
                  if (cmp != 0)
                     return cmp;

                  if (flag != di2.flag)
                     return flag ? 1 : -1;

                  return string.Compare(text2, di2.text2);
               }

               public override string ToString() {
                  return string.Format("{0}, {1}, {2}", text1, flag, text2);
               }
            }


            SortedSet<DataItem> text;
            List<DataItem> table;

            /// <summary>
            /// Zeigt an, ob der <see cref="SimpleTextBag"/> schon abgeschlossen ist.
            /// </summary>
            public bool IsClosed { get; private set; }

            public int Count {
               get {
                  return IsClosed ? table.Count : text.Count;
               }
            }


            public SimpleTextBagExt() {
               text = new SortedSet<DataItem>();
               IsClosed = false;
            }

            /// <summary>
            /// fügt einen Text in den <see cref="SimpleTextBag"/> ein
            /// <para>Ist der <see cref="SimpleTextBag"/> schon abgeschlossen, wird eine Exception ausgelöst.</para>
            /// </summary>
            /// <param name="txt1"></param>
            /// <param name="flag"></param>
            /// <param name="txt2"></param>
            public void Add(string txt1, bool flag = false, string txt2 = "") {
               if (IsClosed)
                  throw new Exception("Der SimpleTextBag ist schon abgeschlossen.");
               DataItem si = new DataItem(txt1, flag, txt2);
               if (!text.Contains(si))
                  text.Add(si);
            }

            /// <summary>
            /// schließt den <see cref="SimpleTextBag"/> ab
            /// </summary>
            public void Close() {
               IsClosed = true;
               table = new List<DataItem>();
               foreach (DataItem si in text)     // sortiert einfügen
                  table.Add(si);
               text.Clear();
            }

            /// <summary>
            /// liefert den Index eines Textes
            /// <para>Ist der <see cref="SimpleTextBag"/> noch nicht abgeschlossen, wird eine Exception ausgelöst.</para>
            /// </summary>
            /// <param name="txt1"></param>
            /// <param name="flag"></param>
            /// <param name="txt2"></param>
            /// <returns></returns>
            public int Index(string txt1, bool flag = false, string txt2 = "") {
               if (!IsClosed)
                  throw new Exception("Der SimpleTextBag ist noch nicht abgeschlossen.");
               return table.BinarySearch(new DataItem(txt1, flag, txt2));
            }

            public string Text1(int idx) {
               return table[idx].text1;
            }

            public string Text2(int idx) {
               return table[idx].text2;
            }

            /// <summary>
            /// liefert die Tabelle aller Texte 1
            /// <para>Ist der <see cref="SimpleTextBag"/> noch nicht abgeschlossen, wird eine Exception ausgelöst.</para>
            /// </summary>
            /// <returns></returns>
            public string[] GetTableTxt1() {
               if (!IsClosed)
                  throw new Exception("Der SimpleTextBag ist noch nicht abgeschlossen.");
               string[] tmp = new string[table.Count];
               int idx = 0;
               foreach (var item in table)
                  tmp[idx++] = item.text1;
               return tmp;
            }

            /// <summary>
            /// liefert die Tabelle aller Texte 3
            /// <para>Ist der <see cref="SimpleTextBag"/> noch nicht abgeschlossen, wird eine Exception ausgelöst.</para>
            /// </summary>
            /// <returns></returns>
            public string[] GetTableTxt2() {
               if (!IsClosed)
                  throw new Exception("Der SimpleTextBag ist noch nicht abgeschlossen.");
               string[] tmp = new string[table.Count];
               int idx = 0;
               foreach (var item in table)
                  tmp[idx++] = item.text2;
               return tmp;
            }

            /// <summary>
            /// liefert die Tabelle aller Flags
            /// <para>Ist der <see cref="SimpleTextBag"/> noch nicht abgeschlossen, wird eine Exception ausgelöst.</para>
            /// </summary>
            /// <returns></returns>
            public bool[] GetTableFlag() {
               if (!IsClosed)
                  throw new Exception("Der SimpleTextBag ist noch nicht abgeschlossen.");
               bool[] tmp = new bool[table.Count];
               int idx = 0;
               foreach (var item in table)
                  tmp[idx++] = item.flag;
               return tmp;
            }

         }

         class PoiIdx : IComparable {

            public byte MainType { get; private set; }
            public byte SubType { get; private set; }
            public ushort SubdivNo { get; private set; }
            public byte PoiListIdx { get; private set; }

            public PoiIdx(byte maintype, byte subtype, ushort subdivno, byte poilistidx) {
               MainType = maintype;
               SubType = subtype;
               SubdivNo = subdivno;
               PoiListIdx = poilistidx;
            }

            public int CompareTo(object obj) {
               PoiIdx pi = obj as PoiIdx;

               if (MainType > pi.MainType)
                  return 1;
               else
                  if (MainType < pi.MainType)
                  return -1;
               if (SubType > pi.SubType)
                  return 1;
               else
                  if (SubType < pi.SubType)
                  return -1;
               if (SubdivNo > pi.SubdivNo)
                  return 1;
               else
                  if (SubdivNo < pi.SubdivNo)
                  return -1;
               if (PoiListIdx > pi.PoiListIdx)
                  return 1;
               else
                  if (PoiListIdx < pi.PoiListIdx)
                  return -1;
               return 0;
            }

            public override string ToString() {
               return string.Format("Typ 0x{0:x}, Subtyp 0x{1:x}, SubdivNo {2}, PoiListIdx {3}", MainType, SubType, SubdivNo, PoiListIdx);
            }
         }

         class PoiExit : IComparable {

            public string Streetname { get; private set; }
            public int RegionIdx { get; private set; }
            public ushort SubdivNo { get; private set; }
            public byte PoiListIdx { get; private set; }

            public PoiExit(string streetname, int regionidx, ushort subdivno, byte poilistidx) {
               Streetname = streetname;
               RegionIdx = regionidx;
               SubdivNo = subdivno;
               PoiListIdx = poilistidx;
            }

            public int CompareTo(object obj) {
               PoiExit pe = obj as PoiExit;

               int cmp = string.Compare(Streetname, pe.Streetname);
               if (cmp != 0)
                  return cmp;
               if (RegionIdx > pe.RegionIdx)
                  return 1;
               else
                  if (RegionIdx < pe.RegionIdx)
                  return -1;
               if (SubdivNo > pe.SubdivNo)
                  return 1;
               else
                  if (SubdivNo < pe.SubdivNo)
                  return -1;
               if (PoiListIdx > pe.PoiListIdx)
                  return 1;
               else
                  if (PoiListIdx < pe.PoiListIdx)
                  return -1;
               return 0;
            }

            public override string ToString() {
               return string.Format("Streetname {0}, RegionIdx {1}, SubdivNo {2}, PoiListIdx {3}", Streetname, RegionIdx, SubdivNo, PoiListIdx);
            }
         }


         StdFile_LBL lbl;
         SimpleTextBag Country, Zip;
         SimpleTextBagExt Region, City;
         /// <summary>
         /// Liste der Punkte im Index
         /// </summary>
         SortedSet<PoiIdx> IdxPoints;
         /// <summary>
         /// Liste der Exit-Punkte
         /// </summary>
         SortedSet<PoiExit> ExitPoints;
         /// <summary>
         /// Zuordnung Straßenname - 1-basierter Index
         /// </summary>
         SortedList<string, ushort> ExitHighwayIndex;


         public HelperLbl(StdFile_LBL lbl) {
            this.lbl = lbl;
            ResetData();
         }

         /// <summary>
         /// setzt die gesamte Datensammlung zurück
         /// </summary>
         public void ResetData() {
            Country = new SimpleTextBag();
            Region = new SimpleTextBagExt();
            City = new SimpleTextBagExt();
            Zip = new SimpleTextBag();
            IdxPoints = new SortedSet<PoiIdx>();
            ExitPoints = new SortedSet<PoiExit>();
            ExitHighwayIndex = new SortedList<string, ushort>();
         }

         /// <summary>
         /// fügt einen einzelnen Text (Label) in die Sammlung ein
         /// </summary>
         /// <param name="label"></param>
         /// <returns></returns>
         bool Add(string label) {
            return lbl.TextList.Insert(label) >= 0;
         }

         /// <summary>
         /// fügt eine Datensammlung ein
         /// </summary>
         /// <param name="country"></param>
         /// <param name="region"></param>
         /// <param name="city"></param>
         /// <param name="zip"></param>
         /// <param name="street"></param>
         /// <param name="streetnumber"></param>
         /// <param name="phonenumber"></param>
         /// <returns></returns>
         bool Add(string country, string region, string city, string zip, string street, string streetnumber, string phonenumber) {
            bool ok = true;

            if (!string.IsNullOrEmpty(country)) {
               ok = lbl.TextList.Insert(country) > 0;
               Country.Add(country);
            }

            if (!string.IsNullOrEmpty(region)) {
               ok = lbl.TextList.Insert(region) > 0;
               Region.Add(region, false, string.IsNullOrEmpty(country) ? "" : country);
            }

            if (!string.IsNullOrEmpty(city)) {
               ok = lbl.TextList.Insert(city) > 0;
               bool isregion = !string.IsNullOrEmpty(region);
               City.Add(city, isregion, isregion ? region : country);
            }

            if (!string.IsNullOrEmpty(zip)) {
               ok = lbl.TextList.Insert(zip) > 0;
               Zip.Add(zip);
            }

            if (!string.IsNullOrEmpty(street)) {
               ok = lbl.TextList.Insert(street) > 0;
               //Street.Add(street);
            }

            if (!string.IsNullOrEmpty(streetnumber)) {
               ok = lbl.TextList.Insert(streetnumber) > 0;
               //StreetNumber.Add(streetnumber);
            }

            if (!string.IsNullOrEmpty(phonenumber)) {
               ok = lbl.TextList.Insert(phonenumber) > 0;
               //PhoneNumber.Add(phonenumber);
            }

            return ok;
         }

         /// <summary>
         /// liefert den 1-basierten Index der Stadt
         /// </summary>
         /// <param name="city">Name der Stadt</param>
         /// <param name="isregion">legt fest, ob <see cref="regionorcountry"/> eine Region oder ein Land ist</param>
         /// <param name="regionorcountry">Region oder Land, zu dem die Stadt gehört</param>
         /// <returns></returns>
         public int Index1ForCity(string city, bool isregion = false, string regionorcountry = "") {
            int idx = City.Index(city, isregion, regionorcountry);
            return idx >= 0 ? idx + 1 : -1;
         }

         /// <summary>
         /// liefert den 1-basierten Index der PLZ
         /// </summary>
         /// <param name="txt"></param>
         /// <returns></returns>
         public int Index1ForZip(string txt) {
            int idx = Zip.Index(txt);
            return idx >= 0 ? idx + 1 : -1;
         }

         /// <summary>
         /// sammelt alle Texte der Detailkarten ein und registriert sie mit ihren Offsets
         /// </summary>
         /// <param name="dmlevellst"></param>
         public void SampleAllText(List<List<DetailMap>> dmlevellst) {
            // Alle Texte werden in der LBL-Datei gespeichert (nichts in der NET-Datei).
            // -> alle Texte im LBL_File-Objekt registrieren, damit die Offsets feststehen
            bool ok;
            do {
               ok = true;
               ResetData();
               Add("");             // "leere" Zeichnekette mit Offset 0

               for (int level = 0; level < dmlevellst.Count && ok; level++) {
                  foreach (DetailMap map in dmlevellst[level]) {
                     if (ok)
                        foreach (DetailMap.Poly poly in map.AreaList) {
                           if (!string.IsNullOrEmpty(poly.Label))
                              ok = Add(poly.Label);
                           if (poly.NetData != null)
                              ok = Add(poly.NetData.Country,
                                       poly.NetData.Region,
                                       poly.NetData.City,
                                       poly.NetData.Zip,
                                       null,
                                       null,
                                       null);
                           if (!ok)
                              break;
                        }

                     if (ok)
                        foreach (DetailMap.Poly poly in map.LineList) {
                           if (!string.IsNullOrEmpty(poly.Label))
                              ok = Add(poly.Label);
                           if (poly.NetData != null)
                              ok = Add(poly.NetData.Country,
                                       poly.NetData.Region,
                                       poly.NetData.City,
                                       poly.NetData.Zip,
                                       null,
                                       null,
                                       null);
                           if (!ok)
                              break;
                        }

                     if (ok)
                        foreach (DetailMap.Point point in map.PointList) {
                           if (!string.IsNullOrEmpty(point.Label))
                              ok = Add(point.Label);
                           if (point.LblData != null) {
                              ok = Add(point.LblData.Country,
                                       point.LblData.Region,
                                       point.LblData.City,
                                       point.LblData.Zip,
                                       point.LblData.Street,
                                       point.LblData.StreetNumber,
                                       point.LblData.PhoneNumber);
                              if (!ok)
                                 break;
                           }

                           if (!ok)
                              break;
                        }
                  }
               }

               if (!ok)
                  lbl.TextList = new StdFile_LBL.TextBag(lbl.TextList.Codec, lbl.TextList.OffsetMultiplier * 2);     // OffsetMultiplier vergrößern und neu versuchen

            } while (!ok);

            Country.Close();
            Region.Close();
            City.Close();
            Zip.Close();

            // Tabellen in der LBL-Datei bilden

            lbl.CountryDataList.Clear();
            foreach (string country in Country.GetTable()) {
               lbl.CountryDataList.Add(new StdFile_LBL.CountryRecord(lbl.GetTextOffset(country)));
            }

            lbl.RegionAndCountryDataList.Clear();
            string[] regions = Region.GetTableTxt1();
            string[] countries = Region.GetTableTxt2();
            for (int i = 0; i < regions.Length; i++) {
               int idx = Country.Index(countries[i]) + 1;
               lbl.RegionAndCountryDataList.Add(new StdFile_LBL.RegionAndCountryRecord((ushort)idx, lbl.GetTextOffset(regions[i])));
            }

            lbl.CityAndRegionOrCountryDataList.Clear();
            string[] citys = City.GetTableTxt1();
            string[] regionsorcountries = City.GetTableTxt2();
            bool[] iscountry = City.GetTableFlag();
            for (int i = 0; i < citys.Length; i++) {
               StdFile_LBL.CityAndRegionOrCountryRecord cr = new StdFile_LBL.CityAndRegionOrCountryRecord();
               cr.TextOffset = lbl.GetTextOffset(citys[i]);
               cr.RegionIsCountry = iscountry[i];
               int idx = 1 + (cr.RegionIsCountry ? Country.Index(regionsorcountries[i]) : Region.Index(regionsorcountries[i]));
               cr.RegionOrCountryIndex = (ushort)idx;
               lbl.CityAndRegionOrCountryDataList.Add(cr);
            }

            lbl.ZipDataList.Clear();
            foreach (string zip in Zip.GetTable()) {
               lbl.ZipDataList.Add(new StdFile_LBL.ZipRecord(lbl.GetTextOffset(zip)));
            }

         }

         /// <summary>
         /// fügt einen Punkt zum Index hinzu
         /// </summary>
         /// <param name="maintype"></param>
         /// <param name="subtype"></param>
         /// <param name="subdivno"></param>
         /// <param name="poilistidx"></param>
         public void AddIndexPoi(byte maintype, byte subtype, ushort subdivno, byte poilistidx) {
            IdxPoints.Add(new PoiIdx(maintype, subtype, subdivno, poilistidx));
         }

         /// <summary>
         /// erzeugt die Index-Listen in der LBL-Datei aus den gesammelten Daten
         /// </summary>
         public void BuildIndexPoiLists() {
            byte maintyp = 0;
            uint idx = 1;            // 1-basierter Index
            foreach (var item in IdxPoints) {
               lbl.PoiIndexDataList.Add(new StdFile_LBL.PoiIndexRecord(item.PoiListIdx, item.SubType, item.SubdivNo));

               if (item.MainType != maintyp) {
                  lbl.PoiTypeIndexDataList.Add(new StdFile_LBL.PoiTypeIndexRecord(item.MainType, idx));
                  maintyp = item.MainType;
               }
               idx++;
            }
         }

         /// <summary>
         /// registriert einen Punkt als Ausfahrt/Kreuzung für Highways
         /// </summary>
         /// <param name="street"></param>
         /// <param name="country"></param>
         /// <param name="region"></param>
         /// <param name="subdivno"></param>
         /// <param name="poilistidx"></param>
         /// <returns>1-basierter Index in der Highway-Tabelle</returns>
         public ushort AddExitPoi(string street, string country, string region, ushort subdivno, byte poilistidx) {
            if (!ExitHighwayIndex.ContainsKey(street))
               ExitHighwayIndex.Add(street, (ushort)(ExitHighwayIndex.Count + 1));

            ExitPoints.Add(new PoiExit(street,
                                       Region.Index(region, false, country) + 1,
                                       subdivno,
                                       poilistidx));

            return ExitHighwayIndex[street];
         }

         /// <summary>
         /// erzeugt die Highway-Exit-Listen in der LBL-Datei aus den gesammelten Daten
         /// </summary>
         public void BuildHighwayExitLists() {
            // damit der Zugriff über Index fkt. ...
            PoiExit[] tab = new PoiExit[ExitPoints.Count];
            ExitPoints.CopyTo(tab);

            int idx = 0;
            bool first = true;
            string oldstreetname = "";
            foreach (var item in ExitPoints) {
               if (first ||
                   oldstreetname != item.Streetname) {
                  lbl.HighwayWithExitList.Add(new StdFile_LBL.HighwayWithExitRecord(lbl.GetTextOffset(item.Streetname), (ushort)(++idx)));
                  lbl.HighwayExitDefList.Add(new StdFile_LBL.HighwayExitDefRecord((ushort)item.RegionIdx));

                  oldstreetname = item.Streetname;
                  first = false;
               }

               StdFile_LBL.HighwayExitDefRecord edr = lbl.HighwayExitDefList[lbl.HighwayExitDefList.Count - 1];
               StdFile_LBL.HighwayExitDefRecord.ExitPoint exp = new StdFile_LBL.HighwayExitDefRecord.ExitPoint(item.PoiListIdx, item.SubdivNo);
               edr.ExitList.Add(exp);

               idx++;
            }
         }

      }

      class HelperTre {

         StdFile_TRE tre;

         public HelperTre(StdFile_TRE tre) {
            this.tre = tre;
         }

         /// <summary>
         /// füllt die Overview-Listen
         /// <para>Die Maplevel-Liste der TRE-Datei muss gefüllt sein.</para>
         /// </summary>
         /// <param name="subdivs">gefüllte (!) Subdiv-Liste der RGN-Datei</param>
         public void SampleOverviewData(IList<StdFile_RGN.SubdivData> subdivs) {
            SortedList<int, byte> MaxSymbolicScale4Area = new SortedList<int, byte>(); // je Objekttyp den max. SymbolicScale ermitteln
            SortedList<int, byte> MaxSymbolicScale4Line = new SortedList<int, byte>();
            SortedList<int, byte> MaxSymbolicScale4Point = new SortedList<int, byte>();

            for (int level = 0; level < tre.SymbolicScaleDenominatorAndBitsLevel.Count; level++) { // für jeden Level (-> SymbolicScale) ...
               byte symbolicscale = (byte)tre.SymbolicScaleDenominatorAndBitsLevel.SymbolicScaleDenominator(level);
               int firstidx = tre.SymbolicScaleDenominatorAndBitsLevel.FirstSubdivChildIdx(level) - 1; // 0-basierter Index
               int endidx = firstidx + tre.SymbolicScaleDenominatorAndBitsLevel.Subdivs(level);

               for (int i = firstidx; i < endidx; i++) { // ... jede Subdiv prüfen 
                  StdFile_RGN.SubdivData sd = subdivs[i];
                  foreach (var obj in sd.AreaList) {
                     int type = (obj.Typ << 8) | obj.Subtyp;
                     if (!MaxSymbolicScale4Area.ContainsKey(type))
                        MaxSymbolicScale4Area.Add(type, symbolicscale);
                  }

                  foreach (var obj in sd.ExtAreaList) {
                     int type = 0x10000 | (obj.Typ << 8) | obj.Subtyp;
                     if (!MaxSymbolicScale4Area.ContainsKey(type))
                        MaxSymbolicScale4Area.Add(type, symbolicscale);
                  }

                  foreach (var obj in sd.LineList) {
                     int type = (obj.Typ << 8) | obj.Subtyp;
                     if (!MaxSymbolicScale4Line.ContainsKey(type))
                        MaxSymbolicScale4Line.Add(type, symbolicscale);
                  }

                  foreach (var obj in sd.ExtLineList) {
                     int type = 0x10000 | (obj.Typ << 8) | obj.Subtyp;
                     if (!MaxSymbolicScale4Line.ContainsKey(type))
                        MaxSymbolicScale4Line.Add(type, symbolicscale);
                  }

                  foreach (var obj in sd.PointList) {
                     int type = (obj.Typ << 8) | obj.Subtyp;
                     if (!MaxSymbolicScale4Point.ContainsKey(type))
                        MaxSymbolicScale4Point.Add(type, symbolicscale);
                  }

                  foreach (var obj in sd.IdxPointList) {
                     int type = (obj.Typ << 8) | obj.Subtyp;
                     if (!MaxSymbolicScale4Point.ContainsKey(type))
                        MaxSymbolicScale4Point.Add(type, symbolicscale);
                  }

                  foreach (var obj in sd.ExtPointList) {
                     int type = 0x10000 | (obj.Typ << 8) | obj.Subtyp;
                     if (!MaxSymbolicScale4Point.ContainsKey(type))
                        MaxSymbolicScale4Point.Add(type, symbolicscale);
                  }
               }
            }

            tre.OverviewsClear();
            foreach (var obj in MaxSymbolicScale4Point)
               tre.OverviewAdd(StdFile_TRE.Overview.Point, obj.Key, obj.Value);
            foreach (var obj in MaxSymbolicScale4Line)
               tre.OverviewAdd(StdFile_TRE.Overview.Line, obj.Key, obj.Value);
            foreach (var obj in MaxSymbolicScale4Area)
               tre.OverviewAdd(StdFile_TRE.Overview.Area, obj.Key, obj.Value);
         }

      }

      class HelperRgn {

         StdFile_RGN rgn;

         public HelperRgn(StdFile_RGN rgn) {
            this.rgn = rgn;
         }

         public void CheckDatasize() {
            foreach (StdFile_RGN.SubdivData sd in rgn.SubdivList) {
               if (sd.DataLength() > 0xFFFF)
                  throw new Exception("Eine RGN_File.Subdiv ist vermutlich zu groß (max. 64kB ?).");

               if (sd.PointList.Count + sd.IdxPointList.Count > 255)
                  throw new Exception("Eine RGN_File.Subdiv enthält vermutlich zu viele 'normale' Punkte (max. 255 ?).");
            }
         }

      }


      /// <summary>
      /// es werden 3 Dateidatenobjekte aus den vorhandenen Daten aufgebaut
      /// </summary>
      /// <param name="tre"></param>
      /// <param name="lbl"></param>
      /// <param name="rgn"></param>
      /// <param name="withpoiidx">bei true wird ein Index für die POI's erzeugt</param>
      protected void SetFileData(StdFile_TRE tre, StdFile_LBL lbl, StdFile_RGN rgn, bool withpoiidx) {
         try {

            List<List<DetailMap>> dmlevellst = new List<List<DetailMap>>();      // Liste der Kartenlisten (ebenenweise, Ebene 0 hat den größten Maßstab)
            for (int level = 0; level < SymbolicScaleDenominatorAndBitsLevel.Count; level++) {
               List<DetailMap> dmlst = GetSubdivmapsWithLevel(level);
               if (dmlst.Count == 1)      // nur eine einzelne Karte
                  dmlst[0].DesiredBounds = new Bound(MapBounds); // Grenzen übernehmen

               dmlevellst.Add(dmlst);
            }

            HelperLbl helperlbl = new HelperLbl(lbl);
            HelperTre helpertre = new HelperTre(tre);

            tre.West = MapBounds.Left;
            tre.East = MapBounds.Right;
            tre.South = MapBounds.Bottom;
            tre.North = MapBounds.Top;

            // alle Texte für die LBL-Datei einsammeln
            helperlbl.SampleAllText(dmlevellst);

            // (die meisten) Daten für die TRE-Datei einsammeln
            tre.MapDescriptionList.Clear();
            for (int i = 0; i < MapDescription.Count; i++)
               tre.MapDescriptionList.Add(MapDescription[i]);

            tre.CopyrightOffsetsList.Clear();
            for (int i = 0; i < Copyright.Count; i++)
               tre.CopyrightOffsetsList.Add((uint)lbl.TextList.Insert(Copyright[i]));

            tre.DisplayPriority = MapLayer;

            tre.MapID = MapID;

            StdFile_TRE.SymbolicScaleDenominatorAndBits tmpsab = new StdFile_TRE.SymbolicScaleDenominatorAndBits();
            for (int level = 0; level < SymbolicScaleDenominatorAndBitsLevel.Count; level++) // Anzahl der Subdivs muss ergänzt werden
               tmpsab.AddLevel(SymbolicScaleDenominatorAndBitsLevel.SymbolicScaleDenominator(level),
                               SymbolicScaleDenominatorAndBitsLevel.Bits(level),
                               dmlevellst[level].Count);
            SymbolicScaleDenominatorAndBitsLevel = tmpsab;
            tre.SymbolicScaleDenominatorAndBitsLevel = new StdFile_TRE.SymbolicScaleDenominatorAndBits(SymbolicScaleDenominatorAndBitsLevel);

            // Subdiv-Daten für die RGN- und 
            // Subdivinfo -Daten für die TRE-Datei einsammeln:
            //    Für jede DetailMap wird ein Subdiv- und ein Subdivinfo-Objekt erzeugt.
            //    Da die Erzeugung ebenenweise erfolgt, sind die Subdivinfo-Objekt automatisch richtig angeordnet.
            for (int level = 0; level < SymbolicScaleDenominatorAndBitsLevel.Count; level++) {
               List<DetailMap> dmlst = dmlevellst[level];
               for (int j = 0; j < dmlst.Count; j++) { // alle DetailMap der Ebene
                  DetailMap dm = dmlst[j];
                  int coordbits = SymbolicScaleDenominatorAndBitsLevel.Bits(level);
                  if (dm.DesiredBounds == null) // d.h., es gibt auch keine Daten
                     continue;

                  // 1. Teil der SubdivInfo aufbauen
                  StdFile_TRE.SubdivInfoBasic sdib = level < tre.SymbolicScaleDenominatorAndBitsLevel.Count - 1 ?
                                                               new StdFile_TRE.SubdivInfo() :
                                                               new StdFile_TRE.SubdivInfoBasic();

                  sdib.Center = dm.DesiredBounds.Center;
                  sdib.HalfHeight = (ushort)(dm.DesiredBounds.HeightRawUnits(coordbits) / 2);
                  sdib.HalfWidth = (ushort)(dm.DesiredBounds.WidthRawUnits(coordbits) / 2);

                  sdib.Content = StdFile_TRE.SubdivInfoBasic.SubdivContent.nothing;
                  if (dm.GetLineTypes(false).Length > 0)
                     sdib.Content |= StdFile_TRE.SubdivInfoBasic.SubdivContent.line;
                  if (dm.GetAreaTypes(false).Length > 0)
                     sdib.Content |= StdFile_TRE.SubdivInfoBasic.SubdivContent.area;
                  if (dm.GetPointTypes(false).Length > 0)
                     sdib.Content |= StdFile_TRE.SubdivInfoBasic.SubdivContent.poi;

                  StdFile_RGN.SubdivData sd = new StdFile_RGN.SubdivData();
                  if (sdib.Content != StdFile_TRE.SubdivInfoBasic.SubdivContent.nothing ||
                      dm.GetLineTypes(true).Length > 0 ||
                      dm.GetAreaTypes(true).Length > 0 ||
                      dm.GetPointTypes(true).Length > 0) {
                     #region Subdiv mit den Objektdaten aufbauen
                     // ================ Polygone verarbeiten

                     foreach (DetailMap.Poly poly in dm.AreaList) {
                        Debug.WriteLineIf(poly.PointCount < 3, string.Format("Fehler: Fläche {0} hat nur {1} Punkt/e", poly, poly.PointCount));
                        if (poly.PointCount > 2)               // min. 3 Punkte
                           if (!poly.IsExtendedType) {
                              StdFile_RGN.RawPolyData polydat = poly.BuildRgnPolyData(sdib.Center, coordbits, true);
                              Bound rb = polydat.GetRawBoundDelta();
                              if (rb.Width == 0 || rb.Height == 0) // bestenfalls ein waagerechter oder senkrechter Strich
                                 continue;
                              polydat.LabelOffset = string.IsNullOrEmpty(poly.Label) ? 0 : lbl.GetTextOffset(poly.Label);
                              sd.AreaList.Add(polydat);
                           } else {
                              StdFile_RGN.ExtRawPolyData polydat = poly.BuildRgnExtPolyData(sdib.Center, coordbits);
                              Bound rb = polydat.GetRawBoundDelta();
                              if (rb.Width == 0 || rb.Height == 0) // bestenfalls ein waagerechter oder senkrechter Strich
                                 continue;
                              polydat.LabelOffset = string.IsNullOrEmpty(poly.Label) ? 0 : lbl.GetTextOffset(poly.Label);
                              sd.ExtAreaList.Add(polydat);
                           }
                     }

                     // ================ Linien verarbeiten

                     foreach (DetailMap.Poly poly in dm.LineList) {
                        Debug.WriteLineIf(poly.PointCount < 2, string.Format("Fehler: Linie {0} hat nur {1} Punkt/e", poly, poly.PointCount));
                        if (poly.PointCount > 1)               // min. 2 Punkte
                           if (!poly.IsExtendedType) {
                              StdFile_RGN.RawPolyData polydat = poly.BuildRgnPolyData(sdib.Center, coordbits, false);
                              Bound rb = polydat.GetRawBoundDelta();
                              if (rb.Width == 0 && rb.Height == 0) // nur ein Punkt
                                 continue;
                              polydat.LabelOffset = string.IsNullOrEmpty(poly.Label) ? 0 : lbl.GetTextOffset(poly.Label);
                              sd.LineList.Add(polydat);
                           } else {
                              StdFile_RGN.ExtRawPolyData polydat = poly.BuildRgnExtPolyData(sdib.Center, coordbits);
                              Bound rb = polydat.GetRawBoundDelta();
                              if (rb.Width == 0 && rb.Height == 0) // nur ein Punkt
                                 continue;
                              polydat.LabelOffset = string.IsNullOrEmpty(poly.Label) ? 0 : lbl.GetTextOffset(poly.Label);
                              sd.ExtLineList.Add(polydat);
                           }
                     }

                     // ================ Punkte verarbeiten

                     uint LbLPoiDataOffset = 0;
                     lbl.POIGlobalMask = StdFile_LBL.POIFlags.nothing;
                     int idxpoints = 0;
                     if (withpoiidx) {       // Anzahl der "City"-POI's bestimmen, weil sie im Index mit berücksichtigt werden müssen
                        foreach (DetailMap.Point point in dm.PointList)
                           if (!point.IsExtendedType &&
                               point.MainType <= 0x11 &&
                               !string.IsNullOrEmpty(point.Label))
                              idxpoints++;
                     }

                     foreach (DetailMap.Point point in dm.PointList) {
                        if (!point.IsExtendedType) {
                           StdFile_RGN.RawPointData pd = new StdFile_RGN.RawPointData();
                           pd.Typ = point.MainType;
                           pd.Subtyp = point.SubType;
                           pd.RawDeltaLatitude = point.Latitude4Save(sdib.Center.Latitude, coordbits);
                           pd.RawDeltaLongitude = point.Longitude4Save(sdib.Center.Longitude, coordbits);
                           bool isexit = false;
                           if (point.LblData != null) {        // es gibt Zusatz-Daten, die in der LBL-Datei gespeichert werden müssen
                              StdFile_LBL.PoiRecord pr = new StdFile_LBL.PoiRecord();

                              if (!string.IsNullOrEmpty(point.LblData.City)) {
                                 pr.CityIndex = (ushort)helperlbl.Index1ForCity(point.LblData.City);
                                 lbl.POIGlobalMask |= StdFile_LBL.POIFlags.city;
                              }
                              if (!string.IsNullOrEmpty(point.LblData.Zip)) {
                                 pr.ZipIndex = (ushort)helperlbl.Index1ForZip(point.LblData.Zip);
                                 lbl.POIGlobalMask |= StdFile_LBL.POIFlags.zip;
                              }
                              if (!string.IsNullOrEmpty(point.LblData.PhoneNumber)) {
                                 pr.PhoneNumberOffset = (ushort)lbl.GetTextOffset(point.LblData.PhoneNumber);
                                 lbl.POIGlobalMask |= StdFile_LBL.POIFlags.phone;
                              }
                              if (!string.IsNullOrEmpty(point.LblData.StreetNumber)) {
                                 pr.StreetNumberOffset = (ushort)lbl.GetTextOffset(point.LblData.StreetNumber);
                                 lbl.POIGlobalMask |= StdFile_LBL.POIFlags.street_num;
                              }
                              if (!string.IsNullOrEmpty(point.LblData.Street)) {
                                 pr.StreetOffset = (ushort)lbl.GetTextOffset(point.LblData.Street);
                                 lbl.POIGlobalMask |= StdFile_LBL.POIFlags.street;
                              }
                              if (!string.IsNullOrEmpty(point.LblData.ExitHighway)) {     // andere Exit-Definition werden wegen Unwissenheit hier nicht behandelt
                                 pr.ExitHighwayIndex = helperlbl.AddExitPoi(point.LblData.ExitHighway,
                                                                            point.LblData.Country,
                                                                            point.LblData.Region,
                                                                            (ushort)(rgn.SubdivList.Count + 1),
                                                                            (byte)(idxpoints + sd.PointList.Count));
                                 isexit = true;
                              }

                              pr.TextOffset = lbl.GetTextOffset(point.LblData.Text);

                              lbl.POIPropertiesList.Add(pr);
                              pd.IsPoiOffset = true;
                              pd.LabelOffset = LbLPoiDataOffset;

                              LbLPoiDataOffset += pr.DataLength(lbl.POIGlobalMask);
                           } else {
                              pd.IsPoiOffset = false;
                           }
                           pd.LabelOffset = string.IsNullOrEmpty(point.Label) ? 0 : lbl.GetTextOffset(point.Label);

                           if (!isexit) {
                              bool added = false;
                              if (withpoiidx &&
                                  !string.IsNullOrEmpty(point.Label)) {       // Punkt muss einen Namen haben
                                 if (point.MainType <= 0x11) {                // "City"-Typen direkt in Subdiv (siehe auch MKGMAP MapBuilder.java)
                                    sd.IdxPointList.Add(pd);
                                    added = true;
                                 } else if (sd.PointList.Count < 256)         // sonst ist der Punktindex zu groß
                                    helperlbl.AddIndexPoi((byte)point.MainType, (byte)point.SubType, (ushort)(rgn.SubdivList.Count + 1), (byte)(idxpoints + sd.PointList.Count));
                              }
                              if (!added)
                                 sd.PointList.Add(pd);
                           }

                        } else {
                           StdFile_RGN.ExtRawPointData pd = new StdFile_RGN.ExtRawPointData();
                           pd.Typ = point.MainType;
                           pd.Subtyp = point.SubType;
                           pd.LabelOffset = string.IsNullOrEmpty(point.Label) ? 0 : lbl.GetTextOffset(point.Label);
                           pd.RawDeltaLatitude = point.Latitude4Save(sdib.Center.Latitude, coordbits);
                           pd.RawDeltaLongitude = point.Longitude4Save(sdib.Center.Longitude, coordbits);
                           sd.ExtPointList.Add(pd);
                        }
                     }
                     #endregion
                  }
                  rgn.SubdivList.Add(sd);

                  if (sdib is StdFile_TRE.SubdivInfo) { // dann auch den Index des 1. untergeordneten Subdiv und die Anzahl der untergeordneten Subdivs speichern (bei SubdivInfoBasic nicht mehr nötig)
                     int childidx = tre.SubdivInfoList.Count + 1; // eigener 1-basierter Index
                     childidx++;
                     childidx += dmlst.Count - j - 1; // restlichen Subdivs der aktuellen Ebene
                     for (int k = 0; k < j; k++) // (j steht für die aktuelle Karte)
                        childidx += dmlst[k].ChildMapCount; // Anzahl der ChildSubddivs der vorherigen Subdivs dieser Ebene

                     StdFile_TRE.SubdivInfo sdi = sdib as StdFile_TRE.SubdivInfo;
                     sdi.ChildSubdivInfos = (ushort)dm.ChildMapCount; // Anzahl der untergeordneten Subdivs
                     sdi.FirstChildSubdivIdx1 = (ushort)childidx; // 1-basierter Index des 1. untergeordneten Subdivs (in der nächsten Ebene)
                  }
                  if (level > 0) {
                     sdib.LastSubdiv = dm.ParentMap.GetChildMap(dm.ParentMap.ChildMapCount - 1) == dm;
                  }

                  tre.SubdivInfoList.Add(sdib);
               }


               //tre.SubdivInfoList.IndexOf()

            }
            helperlbl.BuildIndexPoiLists();
            helperlbl.BuildHighwayExitLists();
            helpertre.SampleOverviewData(rgn.SubdivList);

         } catch (Exception ex) {
            Console.Error.WriteLine("Exception in SimpleMap.SetFileData()");
         }

      }

      #endregion

      /// <summary>
      /// liefert eine <see cref="DetailMap"/> die alle Objekte einer Subdiv-Ebene enthält
      /// <para>Zum Bearbeiten einer eingelesenen Karte sollte i.A. aus der tiefsten Ebene (kleinster Maßstab, größte Bitanzahl) eine Gesamtkarte erzeugt werden. Nach der Bearbeitung
      /// muss diese dann wieder in einen neuen Subdivbaum aufgeteilt werden.</para>
      /// </summary>
      /// <param name="pointtypes4level"></param>
      /// <param name="linetypes4level"></param>
      /// <param name="areatypes4level"></param>
      /// <param name="maplevel"></param>
      /// <returns>null, wenn keine Subdivs oder der gewünschte Level nicht ex./returns>
      public DetailMap BuildMapFromLevel(SortedSet<int> pointtypes4level = null, SortedSet<int> linetypes4level = null, SortedSet<int> areatypes4level = null, int maplevel = int.MaxValue) {
         if (SubdivMaps != null) {
            if (maplevel == int.MaxValue)
               maplevel = SymbolicScaleDenominatorAndBitsLevel.Count - 1;
            if (0 <= maplevel && maplevel <= SymbolicScaleDenominatorAndBitsLevel.Count - 1) {
               DetailMap map = new DetailMap();
               foreach (DetailMap orgmap in GetSubdivmapsWithLevel(maplevel)) {
                  foreach (DetailMap.Point point in orgmap.PointList)
                     if (pointtypes4level == null ||
                         (pointtypes4level != null && pointtypes4level.Contains(point.Type)))
                        map.PointList.Add(point);

                  foreach (DetailMap.Poly line in orgmap.LineList)
                     if (linetypes4level == null ||
                         (linetypes4level != null && linetypes4level.Contains(line.Type)))
                        map.LineList.Add(line);

                  foreach (DetailMap.Poly area in orgmap.AreaList)
                     if (areatypes4level == null ||
                         (areatypes4level != null && areatypes4level.Contains(area.Type)))
                        map.AreaList.Add(area);
               }
               map.DesiredBounds = map.CalculateBounds();
               return map;
            }
         }
         return null;
      }

      /// <summary>
      /// setzt eine Karte, d.h. es wird entsprechend <see cref="SymbolicScaleDenominatorAndBitsLevel"/> und der Objekttypen je Ebene der Subdivbaum erzeugt und gesetzt
      /// </summary>
      /// <param name="map"></param>
      /// <param name="pointtypes4level"></param>
      /// <param name="linetypes4level"></param>
      /// <param name="areatypes4level"></param>
      /// <returns>true, wenn erfolgreich</returns>
      public bool SetMap(DetailMap map, IList<SortedSet<int>> pointtypes4level, IList<SortedSet<int>> linetypes4level, IList<SortedSet<int>> areatypes4level) {
         if (SymbolicScaleDenominatorAndBitsLevel != null &&
             SymbolicScaleDenominatorAndBitsLevel.Count > 0) {

            int[] bits = new int[SymbolicScaleDenominatorAndBitsLevel.Count];
            for (int i = 0; i < SymbolicScaleDenominatorAndBitsLevel.Count; i++)
               bits[i] = SymbolicScaleDenominatorAndBitsLevel.Bits(i);

            if (SymbolicScaleDenominatorAndBitsLevel.Count != pointtypes4level.Count)
               throw new Exception("Die Anzahl der Ebenen stimmt nicht mit der Anzahl der Punkttypebenen überein.");
            if (SymbolicScaleDenominatorAndBitsLevel.Count != linetypes4level.Count)
               throw new Exception("Die Anzahl der Ebenen stimmt nicht mit der Anzahl der Linientypebenen überein.");
            if (SymbolicScaleDenominatorAndBitsLevel.Count != areatypes4level.Count)
               throw new Exception("Die Anzahl der Ebenen stimmt nicht mit der Anzahl der Flächentypebenen überein.");

            SubdivMaps = DetailMapSplitter.BuildSubdivmapMapTree(map, bits, pointtypes4level, linetypes4level, areatypes4level);

            if (SubdivMaps != null && SubdivMaps.Count > 0)
               return true;
         }
         return false;
      }

      public override string ToString() {
         return string.Format("BaseFilename {0}, ID {1}, Level {2}, ChildMaps {3}, Bounds {4}",
                              BaseFilename,
                              MapID,
                              SymbolicScaleDenominatorAndBitsLevel.Count,
                              SubdivMaps.Count,
                              MapBounds);
      }


   }

}
