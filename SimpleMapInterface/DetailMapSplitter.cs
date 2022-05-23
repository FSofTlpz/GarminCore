/*
Copyright (C) 2016 Frank Stinner

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
using ClipperLib;
using GarminCore.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;

using ClipperPath = System.Collections.Generic.List<ClipperLib.IntPoint>;
using ClipperPaths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace GarminCore.SimpleMapInterface {
   public class DetailMapSplitter {

      // from MKGMAP:

      /**
       * Split the area into portions that have the maximum size. There is a maximum limit to the size of a subdivision (16 bits or about 1.4 degrees at the most detailed zoom level).
       *
       * The size depends on the shift level.
       *
       * We are choosing a limit smaller than the real max to allow for uncertainty about what happens with features that extend beyond the box.
       *
       * If the area is already small enough then it will be returned unchanged.
       *
       * @param mapArea The area that needs to be split down.
       * @return An array of map areas.  Each will be below the max size.
       * 
       * 
       * Die halbe Breite und Höhe einer Subdiv wird jeweils in 16 Bit gespeichert. MKGMAP verwendet sicherheitshalber sogar nur 15 Bit (0x7FFF). Die damit erreichbare 
       * max. Breite und Höhe ist zusätzlich abhängig von der Bitanzahl für die Koordinaten 
       * (SymbolicScaleAndBitsLevel.Bits(level) -> SubdivInfoBasic.Degree(rawunits, coordbits) -> GarminCoordinate.RawUnits2Degree(rawunits, coordbits)).
       * Schon daraus folgt, dass ein großes Subdiv bei einem größeren Maßstab/Zoom vermutlich in mehrere Subdiv's aufgeteilt werden muss.
       * 
       * Weiterhin soll die Anzahl der Objekte in einer Subdiv nicht zu groß werden.
       * MKGMAP:  Wenn
       *                Punktanzahl > MAX_NUM_POINTS (0xFF) ||
       *                Linienanzahl > MAX_NUM_LINES (0xFF) ||
       *                (die Summe der geschätzten Datenbereichsgrößen für Punkte, Linien und Flächen) > MAX_RGN_SIZE (0xFFF8) ||
       *                (geschätzten Datenbereichsgrößen für erw. Punkte) > MAX_XT_POINTS_SIZE (0xFF00) ||
       *                (geschätzten Datenbereichsgrößen für erw. Linien) > MAX_XT_LINES_SIZE (0xFF00) ||
       *                (geschätzten Datenbereichsgrößen für erw. Flächen) > MAX_XT_SHAPES_SIZE (0xFF00) ||
       *                Subdiv > WANTED_MAX_AREA_SIZE (0x3FFF) 
       *          dann wir die Subdiv geteilt. Dabei wird je nach dem, welcher Wert größer ist, die Breite oder Höhe halbiert.
       *          Linien und Flächen verteiltt MKGMAP nach der Lage ihrer Mittelpunkte.
       * 
       * -> alle Objekte einer Karte für den akt. Maßstab codieren
       *    Karte (mit den Objekten) rekursiv in Teilkarten aufteilen, wenn 
       *       die Breite und/oder Höhe zu groß ist ||
       *       Punktanzahl > MAX_NUM_POINTS (0xFF) ||
       *       Linienanzahl > MAX_NUM_LINES (0xFF) ||
       *       (die Summe der Datenbereichsgrößen für Punkte, Linien und Flächen) > MAX_RGN_SIZE (0xFFF8) ||
       *       (Datenbereichsgrößen für erw. Punkte) > MAX_XT_POINTS_SIZE (0xFF00) ||
       *       (Datenbereichsgrößen für erw. Linien) > MAX_XT_LINES_SIZE (0xFF00) ||
       *       (Datenbereichsgrößen für erw. Flächen) > MAX_XT_SHAPES_SIZE (0xFF00) ||
       *       Subdiv > WANTED_MAX_AREA_SIZE (0x3FFF)
       *       
       *       
       */

      /// <summary>
      /// liefert die Liste der 'obersten' <see cref="DetailMap"/>'s, die jeweils mit den untergeordneten <see cref="DetailMap"/> verbunden sind und jeweils eine Subdiv repräsentieren
      /// </summary>
      /// <param name="orgmap"></param>
      /// <param name="coordbits">Bitanzahl je Ebene</param>
      /// <param name="pointtypes">erlaubte Punkttypen je Ebene</param>
      /// <param name="linetypes">erlaubte Linientypen je Ebene</param>
      /// <param name="areatypes">erlaubte Flächentypen je Ebene</param>
      /// <returns></returns>
      public static List<DetailMap> BuildSubdivmapMapTree(DetailMap orgmap, int[] coordbits, IList<SortedSet<int>> pointtypes, IList<SortedSet<int>> linetypes, IList<SortedSet<int>> areatypes) {
         //Console.WriteLine("Subdiv-Baum erzeugen ...");
         //livingsign = 0;

         DetailMap firstlevelmap = orgmap.Copy();
         buildSubdivmapTree(orgmap, firstlevelmap, coordbits, pointtypes, linetypes, areatypes);

         // Die Childs der firstlevelmap bilden die Subdivs der obersten Ebene.
         List<DetailMap> result = new List<DetailMap>();
         for (int i = 0; i < firstlevelmap.ChildMapCount; i++) {
            DetailMap sd = firstlevelmap.GetChildMap(i);
            sd.ParentMap = null;
            result.Add(sd);
         }
         //Console.WriteLine();

         return result;
      }

      //static int livingsign = 0;

      /// <summary>
      /// bildet rekursiv den Subdiv-Baum
      /// </summary>
      /// <param name="orgmap"></param>
      /// <param name="parentmap"></param>
      /// <param name="coordbits"></param>
      /// <param name="pointtypes"></param>
      /// <param name="linetypes"></param>
      /// <param name="areatypes"></param>
      /// <param name="level"></param>
      static void buildSubdivmapTree(DetailMap orgmap, DetailMap parentmap, int[] coordbits, IList<SortedSet<int>> pointtypes, IList<SortedSet<int>> linetypes, IList<SortedSet<int>> areatypes, int level = 0) {
         if (level >= coordbits.Length)
            return;

         split2Subdivmaps4Bits(orgmap, parentmap, coordbits[level], pointtypes[level], linetypes[level], areatypes[level]);

         //Console.Write(".");
         //if (++livingsign % 100 == 0)
         //   Console.Write(livingsign);

         level++;
         for (int i = 0; i < parentmap.ChildMapCount; i++)
            buildSubdivmapTree(orgmap, parentmap.GetChildMap(i), coordbits, pointtypes, linetypes, areatypes, level);
      }

      /// <summary>
      /// erzeugt die Subdiv-Maps mit der gewünschten Bitanzahl mit den Objekten aus der Originalkarte im gewünschten Bereich und verknüpft sie mit der übergeordneten Map
      /// </summary>
      /// <param name="orgmap">Originalkarte mit ALLEN Objekten und max. Auflösung</param>
      /// <param name="parentmap"></param>
      /// <param name="coordbits">Bitanzahl</param>
      /// <param name="pointtypes">erlaubte Punkttypen</param>
      /// <param name="linetypes">erlaubte Linientypen</param>
      /// <param name="areatypes">erlaubte Flächentypen</param>
      static void split2Subdivmaps4Bits(DetailMap orgmap, DetailMap parentmap, int coordbits, SortedSet<int> pointtypes, SortedSet<int> linetypes, SortedSet<int> areatypes) {
         coordbits = Math.Max(10, Math.Min(coordbits, 24)); // eingrenzen 10 .. 24

         DetailMap map4bits = PrepareMap(orgmap, coordbits, parentmap.DesiredBounds, pointtypes, linetypes, areatypes);
         List<DetailMap> subdivmaps = splitDetailMap(map4bits, coordbits, 0, 0);
         if (subdivmaps == null)
            subdivmaps = new List<DetailMap>() { map4bits };

         while (parentmap.ChildMapCount > 0) // ev. vorhandene Childs lösen
            parentmap.GetChildMap(0).ParentMap = null;

         foreach (var item in subdivmaps)
            item.ParentMap = parentmap;
      }


      // größere Werte können Probleme bei der Codierung der Delta-Werte ergeben
      //const int MAX_BOUND4LINE = 0x7FFF; // MKGMAP; etwa 0.703° bei 24 Bit
      //const int MAX_BOUND4AREA = 0xFFFF; // MKGMAP; etwa 1.406° bei 24 Bit
      const int MAX_BOUND4LINE = 0x7FFF;
      const int MAX_BOUND4AREA = 0x3FFF;
      /// <summary>
      /// kleinste Maximalgröße
      /// </summary>
      const int MIN_4MAX_BOUND = 0x7FFF; // MKGMAP 0x8000

      /// <summary>
      /// erzeugt eine Kopie der Originalkarte und teilt Objekte, die für die angegebene Bitanzahl zu groß sind auf
      /// <para>Dabei werden nur die gewünschten Objekttypen übernommen und es bleiben nur Objekte übrig, die eine Schnittmenge mit der gewünschten Umgrenzung haben.</para>
      /// </summary>
      /// <param name="orgmap"></param>
      /// <param name="coordbits"></param>
      /// <param name="destbound">nur Objekte, die diesen Bereich berühren</param>
      /// <param name="pointtypes">erlaubte Punkttypen</param>
      /// <param name="linetypes">erlaubte Linientypen</param>
      /// <param name="areatypes">erlaubte Flächentypen</param>
      /// <returns></returns>
      static DetailMap PrepareMap(DetailMap orgmap, int coordbits, Bound destbound, SortedSet<int> pointtypes, SortedSet<int> linetypes, SortedSet<int> areatypes) {
         DetailMap newmap = orgmap.Copy(destbound, false, pointtypes, linetypes, areatypes);

         int max_bound4line = Math.Min((1 << 24) - 1, Math.Max(MAX_BOUND4LINE << (24 - coordbits), MIN_4MAX_BOUND)); // MAX_BOUND4LINE << (24 - coordbits), aber eingegrenzt auf 0x8000 .. 0xFFFFFF
         int max_bound4area = Math.Min((1 << 24) - 1, Math.Max(MAX_BOUND4AREA << (24 - coordbits), MIN_4MAX_BOUND));
         /* coordbits  max_bound4line
                        Linie       Fläche
                  Res   maxSize     maxSize
                  24    0x007FFF    0x00FFFF
                  23    0x00FFFF    0x01FFFF
                  22    0x01FFFF    0x03FFFF
                  21    0x03FFFF    0x07FFFF
                  20    0x07FFFF    0x0FFFFF
                  19    0x0FFFFF    0x1FFFFF
                  18    0x1FFFFF    0x3FFFFF
                  17    0x3FFFFF    0x7FFFFF
                  16    0x7FFFFF    0xFFFFFF
                  15    0xFFFFFF    "

            Bei weniger als 15 Bit ist keine Aufteilung mehr nötig.
          */

         // Linien und Flächen die zu groß sind, aufteilen
         List<DetailMap.Poly> lines = new List<DetailMap.Poly>();
         foreach (var item in newmap.LineList)
            lines.AddRange(MakeSafeLines(item, max_bound4line));       // 0.7° bei 24 Bit (je 1 Bit weniger verdoppeln)
         newmap.LineList.Clear();
         newmap.LineList.AddRange(lines);

         List<DetailMap.Poly> areas = new List<DetailMap.Poly>();
         foreach (var item in newmap.AreaList)
            areas.AddRange(MakeSafeAreas(item, max_bound4area));
         newmap.AreaList.Clear();
         newmap.AreaList.AddRange(areas);

         // vollständig außerhalb des Zielbereiches liegende Objekte entfernen
         for (int i = 0; i < newmap.PointList.Count; i++)
            if (!destbound.IsEnclosed(newmap.PointList[i].Coordinates)) // liegt außerhalb des Zielbereiches
               newmap.PointList.RemoveAt(i--);
         for (int i = 0; i < newmap.LineList.Count; i++)
            if (destbound.Intersection(newmap.LineList[i].Bound) == null) // liegt vollständig außerhalb des Zielbereiches
               newmap.LineList.RemoveAt(i--);
         for (int i = 0; i < newmap.AreaList.Count; i++)
            if (destbound.Intersection(newmap.AreaList[i].Bound) == null) // liegt vollständig außerhalb des Zielbereiches
               newmap.AreaList.RemoveAt(i--);

         return newmap;
      }

      // Not sure of the value, probably 255.  Say 250 here.
      const int MAX_POINTS_IN_LINE = 250;
      const int MIN_POINTS_IN_LINE = 50;

      /// <summary>
      /// "sichere" Linien erzeugen (ev. aufteilen)
      /// </summary>
      /// <param name="line"></param>
      /// <param name="maxsize">max. "quadratisches" Umgrenzung in Grad</param>
      /// <returns></returns>
      static List<DetailMap.Poly> MakeSafeLines(DetailMap.Poly line, int maxsize) {
         List<DetailMap.Poly> lst = new List<DetailMap.Poly>();

         // zwischen 2 zu weit entfernte Punkte wird ein neuer Punkt eingefügt
         MapUnitPoint p2 = line.PointCount > 1 ? line.GetPoint(0) : null;
         for (int i = 0; i < line.PointCount - 1; i++) {
            MapUnitPoint p1 = p2;
            if (p1 != null) {
               p2 = line.GetPoint(i + 1);
               if (Math.Abs(p2.Longitude - p1.Longitude) > maxsize ||
                   Math.Abs(p2.Latitude - p1.Latitude) > maxsize) {
                  line.AddPoint(p1 + (p2 - p1) / 2, false, true, i + 1);
                  i--;
                  p2 = p1;
               }
            }
         }

         lst.Add(line);

         // sind zu viele Punkte (in der letzten Linie der Liste) vorhanden, wird die Linie geteilt
         if (lst[lst.Count - 1].PointCount > MAX_POINTS_IN_LINE) {
            DetailMap.Poly line1 = lst[lst.Count - 1];
            int wantedSize = (line1.PointCount < MAX_POINTS_IN_LINE + MIN_POINTS_IN_LINE) ?
                                                            line1.PointCount / 2 + 10 :
                                                            MAX_POINTS_IN_LINE;
            lst.Add(splitPoly(line1, wantedSize - 1));
         }

         // Alle Linien der Liste haben jetzt weder zu viele Punkte noch zu weit entfernte Punkte.

         for (int i = 0; i < lst.Count; i++) {
            DetailMap.Poly line1 = lst[i];
            if (line1.Bound.Width > maxsize ||
                line1.Bound.Height > maxsize) { // Aufteilung ist nötig
               Bound testbound = new Bound(line1.GetPoint(0));
               for (int j = 1; j < line1.PointCount; j++) {
                  testbound.Embed(line1.GetPoint(j));
                  if (testbound.Width > maxsize ||
                      testbound.Height > maxsize) {
                     DetailMap.Poly line2 = splitPoly(line1, j - 1);
                     lst.Insert(i + 1, line2); // wird als nächste getestet
                     break;
                  }
               }
            }
         }

         return lst;
      }

      /// <summary>
      /// trennt das Polygon am bezeichneten Punkt, so dass dieser sowohl noch zum alten als auch zum neuen Polygon gehört
      /// </summary>
      /// <param name="poly"></param>
      /// <param name="idx"></param>
      /// <returns></returns>
      static DetailMap.Poly splitPoly(DetailMap.Poly poly, int idx) {
         DetailMap.Poly poly2 = new DetailMap.Poly(poly);
         poly.RemoveRangeOfPoints(idx + 1, poly.PointCount - idx - 1);
         poly2.RemoveRangeOfPoints(0, idx); // 1 gemeinsamer Punkt bleibt !
         return poly2;
      }

      /// <summary>
      /// "sichere" Flächen erzeugen (ev. aufteilen)
      /// </summary>
      /// <param name="area"></param>
      /// <param name="maxsize">max. "quadratisches" Umgrenzung in MapUnits</param>
      /// <returns></returns>
      static List<DetailMap.Poly> MakeSafeAreas(DetailMap.Poly area, int maxsize) {
         List<DetailMap.Poly> lst = new List<DetailMap.Poly>();

         ClipperPath p = MapUnitPointList2ClipperPath(area.GetMapUnitPoints());
         bool counterclockwise = Clipper.Orientation(p);

         ClipperPaths newpolys = SplitClipperPath(MapUnitPointList2ClipperPath(area.GetMapUnitPoints()), maxsize);
         for (int i = 0; i < newpolys.Count; i++)
            if (Clipper.Orientation(newpolys[i]) != counterclockwise)
               newpolys[i].Reverse();

         if (newpolys.Count > 1) { // es ist eine Teilung erfolgt
            foreach (var newpoly in newpolys) {
               DetailMap.Poly newarea = new DetailMap.Poly(area);
               newarea.Clear();
               foreach (var pt in newpoly)
                  newarea.AddPoint(new MapUnitPoint((int)pt.X, (int)pt.Y), false);
               lst.Add(newarea);
            }
         } else
            lst.Add(area);

         return lst;
      }

      #region Hilfsfunktionen der ClipperLib für das Teilen von Flächen

      /// <summary>
      /// liefert eine Liste Pfaden (min. aber den Originalpfad), die die max. Größe nicht überschreiten
      /// </summary>
      /// <param name="poly"></param>
      /// <param name="maxsize"></param>
      /// <returns></returns>
      static ClipperPaths SplitClipperPath(ClipperPath poly, int maxsize) {
         ClipperPaths result = new ClipperPaths();

         GetBounding4ClipperPath(poly, out int left, out int bottom, out int width, out int height);

         /* Wenn Gebiet zu groß:
          * Halbierung der Boundingbox an ihrer größeren Seite
          * entsprechende Halbierung der Fläche
          */

         if (width > maxsize || height > maxsize) {
            ClipperPath rect1 = new ClipperPath();
            ClipperPath rect2 = new ClipperPath();
            if (width > height) {
               rect1.Add(new IntPoint(left, bottom + height));
               rect1.Add(new IntPoint(left + width / 2, bottom + height));
               rect1.Add(new IntPoint(left + width / 2, bottom));
               rect1.Add(new IntPoint(left, bottom));

               rect2.Add(new IntPoint(left + width / 2, bottom + height));
               rect2.Add(new IntPoint(left + width, bottom + height));
               rect2.Add(new IntPoint(left + width, bottom));
               rect2.Add(new IntPoint(left + width / 2, bottom));
            } else {
               rect1.Add(new IntPoint(left, bottom + height));
               rect1.Add(new IntPoint(left + width, bottom + height));
               rect1.Add(new IntPoint(left + width, bottom + height / 2));
               rect1.Add(new IntPoint(left, bottom + height / 2));

               rect2.Add(new IntPoint(left, bottom + height / 2));
               rect2.Add(new IntPoint(left + width, bottom + height / 2));
               rect2.Add(new IntPoint(left + width, bottom));
               rect2.Add(new IntPoint(left, bottom));
            }
            Clipper c = new Clipper();

            c.AddPath(poly, PolyType.ptSubject, true);
            c.AddPath(rect1, PolyType.ptClip, true);
            ClipperPaths poly1 = new ClipperPaths();
            c.Execute(ClipType.ctIntersection, poly1);

            c.Clear();

            c.AddPath(poly, PolyType.ptSubject, true);
            c.AddPath(rect2, PolyType.ptClip, true);
            ClipperPaths poly2 = new ClipperPaths();
            c.Execute(ClipType.ctIntersection, poly2);

            poly1.AddRange(poly2);

            foreach (var item in poly1)
               result.AddRange(SplitClipperPath(item, maxsize));
         } else
            result.Add(poly);
         return result;
      }

      /// <summary>
      /// erzeugt aus einer <see cref="DetailMap.MapUnitPoint"/>-Liste einen Pfad für das Clipping
      /// </summary>
      /// <param name="mupt"></param>
      /// <returns></returns>
      static ClipperPath MapUnitPointList2ClipperPath(List<MapUnitPoint> mupt) {
         ClipperPath poly = new ClipperPath();
         foreach (var item in mupt)
            poly.Add(new IntPoint(item.Longitude, item.Latitude));
         return poly;
      }

      /// <summary>
      /// ersetzt die Polyline in <see cref="DetailMap.Poly"/> durch den Pfad vom Clipping (in MapUnits)
      /// </summary>
      /// <param name="path"></param>
      /// <param name="area"></param>
      static void SetClipperPath2Area(ClipperPath path, DetailMap.Poly area) {
         area.Clear();
         foreach (var item in path)
            area.AddPoint(new MapUnitPoint(item.X, item.Y), false);
      }

      /// <summary>
      /// liefert die Breite und Höhe des Pfades
      /// </summary>
      /// <param name="path"></param>
      /// <param name="left"></param>
      /// <param name="bottom"></param>
      /// <param name="width"></param>
      /// <param name="height"></param>
      static void GetBounding4ClipperPath(ClipperPath path, out int left, out int bottom, out int width, out int height) {
         int minx = int.MaxValue;
         int maxx = int.MinValue;
         int miny = int.MaxValue;
         int maxy = int.MinValue;
         foreach (var item in path) {
            minx = Math.Min(minx, (int)item.X);
            maxx = Math.Max(maxx, (int)item.X);
            miny = Math.Min(miny, (int)item.Y);
            maxy = Math.Max(maxy, (int)item.Y);
         }
         left = minx;
         bottom = miny;
         width = maxx - minx;
         height = maxy - miny;
      }

      #endregion


      /* tmp bildet jetzt die Root des Teilkartenbaumes. 
         Jede "Ebene" des Baums ist für einen Zoomlevel zuständig. 
         Je stärker ge"zoomt" wird, je höher ist die Ebene.

         *  *  *  *  *  *  *     2
          \ |  |  |  |  |  |
           \|  |  |  |  |  |
            *  *  *  *  *  *     1
             \ | /   |  | /
              \|/    |  |/
               *     *  *        0
                \    | /
                 \  / /
                  \/ /
                   \/
                   *             Originalkarte
      */

      /// <summary>
      /// teilt eine einzelne (zu große) Karte in eine Liste von mindestens <see cref="width_divider"/> * <see cref="height_divider"/> Karten auf
      /// </summary>
      /// <param name="map"></param>
      /// <param name="coordbits"></param>
      /// <param name="width_divider">Teile in waagerechter Richtung</param>
      /// <param name="height_divider">Teile in senkrechter Richtung</param>
      /// <returns>null, wenn keine Aufteilung nötig ist</returns>
      static List<DetailMap> splitDetailMap(DetailMap map, int coordbits, int width_divider, int height_divider) {
         List<DetailMap> newmaps = new List<DetailMap>();

         if (width_divider <= 1 &&
             height_divider <= 1) {

            if (!splitIsNeeded(map, coordbits, out width_divider, out height_divider))
               return null;
            return splitDetailMap(map, coordbits, width_divider, height_divider);

         } else {

            Bound RawBound = map.DesiredBounds.AsRawUnitBound(coordbits);

            int halfwidth_rawunits = (RawBound.Width + 1) / 2; // der Wert ist auf keinen Fall zu klein, höchstens um +1 zu groß
            int halfheight_rawunits = (RawBound.Height + 1) / 2;

            int new_halfwidth_rawunits = halfwidth_rawunits / width_divider;
            int new_halfheight_rawunits = halfheight_rawunits / height_divider;
            if (new_halfwidth_rawunits * width_divider < halfwidth_rawunits)
               new_halfwidth_rawunits++; // new_halfwidth_rawunits * width_divider deckt mit Sicherheit die Breite ab
            if (new_halfheight_rawunits * height_divider < halfheight_rawunits)
               new_halfheight_rawunits++;

            for (int i = 0; i < width_divider; i++) {
               for (int j = 0; j < height_divider; j++) {
                  int l = RawBound.Left + 2 * new_halfwidth_rawunits * i;
                  int r = l + 2 * new_halfwidth_rawunits;
                  int b = RawBound.Bottom + 2 * new_halfheight_rawunits * j;
                  int t = b + 2 * new_halfheight_rawunits;

                  newmaps.Add(new DetailMap(null, new Bound(l, r, b, t, coordbits)));
               }
            }
         }

         // ----- alle Teilkarten mit Objekten entsprechend ihrer Grenzen füllen

         foreach (DetailMap.Point pt in map.PointList) {
            int xcell = (int)((pt.Coordinates.Longitude - map.DesiredBounds.Left) / newmaps[0].DesiredBounds.Width);
            int ycell = (int)((pt.Coordinates.Latitude - map.DesiredBounds.Bottom) / newmaps[0].DesiredBounds.Height);
            newmaps[ycell * width_divider + xcell].PointList.Add(pt);
         }

         // Die Breite und Höhe der Karten ist für alle Karten gleich.
         int maxHeight = newmaps[0].DesiredBounds.Height;
         int maxWidth = newmaps[0].DesiredBounds.Width;

         //const int LARGE_OBJECT_DIM = 0x2000;
         //if (width_divider * height_divider == 1 ||
         //    maxWidth < LARGE_OBJECT_DIM ||
         //    maxHeight < LARGE_OBJECT_DIM) {
         //   maxWidth = double.MaxValue;
         //   maxHeight = double.MaxValue;
         //}

         foreach (DetailMap.Poly poly in map.LineList) {
            int xcell = (int)((poly.Bound.CenterX - map.DesiredBounds.Left) / maxWidth);
            int ycell = (int)((poly.Bound.CenterY - map.DesiredBounds.Bottom) / maxHeight);
            int idx = ycell * width_divider + xcell;
            if (!newmaps[idx].DesiredBounds.IsEnclosed(poly.Bound)) {
               if (!map.DesiredBounds.IsEnclosed(poly.Bound))
                  throw new Exception(string.Format("Die Line ist zu groß für eine Subdiv bei {0} Bits: {1}", coordbits, poly.ToStringExt()));
               newmaps.Add(new DetailMap(null, poly.Bound)); // zusätzliche "große" Karte für ein Einzelobjekt anhängen
               idx = newmaps.Count - 1;
            }
            newmaps[idx].LineList.Add(poly);
         }

         foreach (DetailMap.Poly poly in map.AreaList) {
            int xcell = (int)((poly.Bound.CenterX - map.DesiredBounds.Left) / maxWidth);
            int ycell = (int)((poly.Bound.CenterY - map.DesiredBounds.Bottom) / maxHeight);
            int idx = ycell * width_divider + xcell;
            if (!newmaps[idx].DesiredBounds.IsEnclosed(poly.Bound)) {
               if (!map.DesiredBounds.IsEnclosed(poly.Bound))
                  throw new Exception(string.Format("Die Fläche ist zu groß für eine Subdiv bei {0} Bits: {1}", coordbits, poly.ToStringExt()));
               newmaps.Add(new DetailMap(null, poly.Bound)); // zusätzliche "große" Karte anhängen
               idx = newmaps.Count - 1;
            }
            newmaps[idx].AreaList.Add(poly);
         }

         // ----- für jede neue Karte (rekursiv) testen, ob eine weitere Teilung nötig ist; dadurch kann sich die Liste noch verlängern
         for (int i = 0; i < newmaps.Count; i++) {
            if (splitIsNeeded(newmaps[i], coordbits, out width_divider, out height_divider)) {
               List<DetailMap> newmaps2 = splitDetailMap(newmaps[i], coordbits, width_divider, height_divider);
               newmaps.RemoveAt(i);
               newmaps.InsertRange(i, newmaps2);
            }
         }

         return newmaps;
      }

      /// <summary>
      /// There is an absolute largest size as offsets are in 16 bits, we are staying safely inside it however.
      /// </summary>
      const int MAX_DIVISION_SIZE = 0x7fff;
      /// <summary>
      /// max. (geografische) Länge/Breite einer Subdiv
      /// </summary>
      const int MAX_SUBDIV_HALFSIZE = 0x7FFF;
      /// <summary>
      /// max. Anzahl Punkte in Subdiv
      /// </summary>
      const int MAX_NUM_POINTS = 0xff;
      /// <summary>
      /// max. Anzahl Linien in Subdiv
      /// </summary>
      const int MAX_NUM_LINES = 0xff;
      /// <summary>
      /// The maximum region size. Note that the offset to the start of a section has to fit into 16 bits, the end of the last section could be beyond the 16 bit limit. 
      /// Leave a little room for the region pointers.
      /// </summary>
      const int MAX_RGN_SIZE = 0xfff8;
      /// <summary>
      /// maximum allowed amounts of points with extended types;
      /// real limits are not known but if these values are too large, data goes missing (lines disappear, etc.)
      /// </summary>
      const int MAX_XT_POINTS_SIZE = 0xff00;
      /// <summary>
      /// maximum allowed amounts of lines with extended types;
      /// real limits are not known but if these values are too large, data goes missing (lines disappear, etc.)
      /// </summary>
      const int MAX_XT_LINES_SIZE = 0xff00;
      /// <summary>
      /// maximum allowed amounts of shapes with extended types;
      /// real limits are not known but if these values are too large, data goes missing (lines disappear, etc.)
      /// </summary>
      const int MAX_XT_SHAPES_SIZE = 0xff00;

      /// <summary>
      /// Test, ob die Karte (bei der vorgegebenen Bitanzahl) geteilt werden muss
      /// </summary>
      /// <param name="map"></param>
      /// <param name="coordbits"></param>
      /// <param name="width_divider">Teiler für die Breite</param>
      /// <param name="height_divider">Teiler für die Höhe</param>
      /// <returns></returns>
      static bool splitIsNeeded(DetailMap map, int coordbits, out int width_divider, out int height_divider) {
         bool isneeded = false;

         width_divider = 1;
         height_divider = 1;

         int halfwidth_rawunits = map.DesiredBounds.WidthRawUnits(coordbits) / 2;
         int halfheight_rawunits = map.DesiredBounds.HeightRawUnits(coordbits) / 2;
         if (halfwidth_rawunits > MAX_SUBDIV_HALFSIZE)
            width_divider = halfwidth_rawunits / MAX_SUBDIV_HALFSIZE + 1; // nur ganzzahlige Teiler sinnvoll
         if (halfheight_rawunits > MAX_SUBDIV_HALFSIZE)
            height_divider = halfheight_rawunits / MAX_SUBDIV_HALFSIZE + 1;

         if (width_divider > 1 ||
             height_divider > 1)
            isneeded = true; // Breite oder Höhe ist zu groß

         else {
            if (map.PointCount(false) > MAX_NUM_POINTS)
               isneeded = true; // zuviele Punkte
            else if (map.LineCount(false) > MAX_NUM_LINES)
               isneeded = true; // zuviele Linien

            else {

               // Speicherplatzgrößen bestimmen
               StdFile_TRE.SubdivInfoBasic sdib = new StdFile_TRE.SubdivInfoBasic {
                  Center = new MapUnitPoint(map.DesiredBounds.Left + map.DesiredBounds.Width / 2, map.DesiredBounds.Bottom + map.DesiredBounds.Height / 2)
               };

               uint point_datalength = 0;
               uint extpoint_datalength = 0;
               foreach (DetailMap.Point point in map.PointList) {
                  if (!point.IsExtendedType) {
                     StdFile_RGN.RawPointData pd = new StdFile_RGN.RawPointData {
                        Type = point.MainType,
                        Subtype = point.SubType
                     };
                     point_datalength += pd.DataLength;
                  } else {
                     StdFile_RGN.ExtRawPointData pd = new StdFile_RGN.ExtRawPointData {
                        Type = point.MainType,
                        Subtype = point.SubType,
                        LabelOffsetInLBL = (uint)(string.IsNullOrEmpty(point.Label) ? 0 : 1)
                     };
                     // ev. noch pd.ExtraBytes setzen
                     extpoint_datalength += pd.DataLength;
                  }
               }

               uint line_datalength = 0;
               uint extline_datalength = 0;
               foreach (DetailMap.Poly poly in map.LineList) {
                  Debug.WriteLineIf(poly.PointCount < 2, string.Format("Fehler: Linie {0} hat nur {1} Punkt/e", poly, poly.PointCount));
                  if (poly.PointCount > 1)               // min. 2 Punkte
                     if (!poly.IsExtendedType) {
                        StdFile_RGN.RawPolyData polydat = poly.BuildRgnPolyData(sdib.Center, coordbits, false);
                        polydat.LabelOffsetInLBL = (uint)(string.IsNullOrEmpty(poly.Label) ? 0 : 1);
                        line_datalength += polydat.DataLength;
                     } else {
                        StdFile_RGN.ExtRawPolyData polydat = poly.BuildRgnExtPolyData(sdib.Center, coordbits);
                        polydat.LabelOffsetInLBL = (uint)(string.IsNullOrEmpty(poly.Label) ? 0 : 1);
                        extline_datalength += polydat.DataLength;
                     }
               }

               uint area_datalength = 0;
               uint extarea_datalength = 0;
               foreach (DetailMap.Poly poly in map.AreaList) {
                  Debug.WriteLineIf(poly.PointCount < 3, string.Format("Fehler: Fläche {0} hat nur {1} Punkt/e", poly, poly.PointCount));
                  if (poly.PointCount > 2)               // min. 3 Punkte
                     if (!poly.IsExtendedType) {
                        StdFile_RGN.RawPolyData polydat = poly.BuildRgnPolyData(sdib.Center, coordbits, false);
                        polydat.LabelOffsetInLBL = (uint)(string.IsNullOrEmpty(poly.Label) ? 0 : 1);
                        area_datalength += polydat.DataLength;
                     } else {
                        StdFile_RGN.ExtRawPolyData polydat = poly.BuildRgnExtPolyData(sdib.Center, coordbits);
                        polydat.LabelOffsetInLBL = (uint)(string.IsNullOrEmpty(poly.Label) ? 0 : 1);
                        extarea_datalength += polydat.DataLength;
                     }
               }

               if (point_datalength + extpoint_datalength +
                   line_datalength + extline_datalength +
                   area_datalength + extarea_datalength > MAX_RGN_SIZE)
                  isneeded = true; // Datenbereich insgesamt zu groß

               else if (extpoint_datalength > MAX_XT_POINTS_SIZE)
                  isneeded = true; // Datenbereich für erweiterte Punkte zu groß

               else if (extline_datalength > MAX_XT_LINES_SIZE)
                  isneeded = true; // Datenbereich für erweiterte Linien zu groß

               else if (extarea_datalength > MAX_XT_SHAPES_SIZE)
                  isneeded = true; // Datenbereich für erweiterte Flächen zu groß
            }
         }

         if (isneeded) { // Vierteilung
            width_divider = 2;
            height_divider = 2;
         }

         return isneeded;
      }


   }
}
