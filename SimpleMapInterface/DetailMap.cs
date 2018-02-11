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
using GarminCore.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GarminCore.SimpleMapInterface {

   /// <summary>
   /// eine Teilkarte mit allen Objekten (entspricht einer <see cref="StdFile_RGN.SubdivData"/>)
   /// <para>alle Koordinaten in Grad</para>
   /// <para>Eine vollständige Pseudo-Garmin-Karte besteht aus einer Baumstruktur aus <see cref="DetailMap"/>-Objekten. Die Ebenen im
   /// Baum stehen für die Maplevel.</para>
   /// </summary>
   public class DetailMap {

      /// <summary>
      /// zusätzliche Daten für normale Punkte (aus der LBL-Datei)
      /// </summary>
      public class PoiDataExt {

         public string Text;
         public string Country;
         public string Region;
         public string City;
         public string Zip;
         public string Street;
         public string StreetNumber;
         public string PhoneNumber;
         public string ExitHighway;
         public int ExitOffset;
         public int ExitIndex;

         public PoiDataExt(StdFile_LBL.PoiRecord data, StdFile_LBL lbl) {
            Text = Country = Region = City = Zip = Street = StreetNumber = PhoneNumber = ExitHighway = "";
            ExitOffset = ExitIndex = -1;

            if (data.TextOffset > 0)
               Text = lbl.GetText(data.TextOffset);

            if (data.ZipIsSet)
               Zip = lbl.GetText(lbl.ZipDataList[data.ZipIndex - 1].TextOffset);

            if (data.CityIsSet) {
               StdFile_LBL.CityAndRegionOrCountryRecord cr = lbl.CityAndRegionOrCountryDataList[data.CityIndex - 1];

               if (!cr.IsPOI)
                  City = lbl.GetText(cr.TextOffset);
               else {

                  if (cr.POIIndex != 0 && cr.SubdivisionNumber != 0)
                     Debug.WriteLineIf(cr.POIIndex != 0 && cr.SubdivisionNumber != 0,
                                       string.Format("POIIndex {0}, SubdivisionNumber {1}", cr.POIIndex, cr.SubdivisionNumber));

               }

               if (cr.RegionIsCountry) {
                  Country = lbl.GetText(lbl.CountryDataList[cr.RegionOrCountryIndex - 1].TextOffset);
               } else {
                  Region = lbl.GetText(lbl.RegionAndCountryDataList[cr.RegionOrCountryIndex - 1].TextOffset);
                  Country = lbl.GetText(lbl.CountryDataList[lbl.RegionAndCountryDataList[cr.RegionOrCountryIndex - 1].CountryIndex - 1].TextOffset);
               }
            }

            if (data.StreetIsSet)
               Street = lbl.GetText(data.StreetOffset);

            if (data.StreetNumberIsSet)
               if (data.StreetNumberIsCoded)
                  StreetNumber = data.StreetNumber;
               else
                  StreetNumber = lbl.GetText(data.StreetNumberOffset);

            if (data.PhoneIsSet)
               if (data.PhoneNumberIsCoded)
                  StreetNumber = data.PhoneNumber;
               else
                  StreetNumber = lbl.GetText(data.PhoneNumberOffset);

            if (data.ExitIsSet) {
               if (data.ExitIndexIsSet) {
                  StdFile_LBL.ExitRecord er = lbl.ExitList[data.ExitIndex];
                  ExitIndex = data.ExitIndex;

                  Debug.WriteLine("ExitIndex {0}; Direction {1}, Type {2}, Facilities {3}, LastFacilitie {4}, Text {5}",
                                    data.ExitIndex,
                                    er.Direction,
                                    er.Type,
                                    er.Facilities,
                                    er.LastFacilitie,
                                    lbl.GetText(er.TextOffset));

               } else if (data.ExitHighwayIndex != 0xFFFF) {
                  if (0 < data.ExitHighwayIndex && data.ExitHighwayIndex <= lbl.HighwayWithExitList.Count)
                     ExitHighway = lbl.GetText(lbl.HighwayWithExitList[data.ExitHighwayIndex - 1].TextOffset);

                  else
                     Debug.WriteLine("ExitHighwayIndex {0}, ExitOffset {1} (LBL_File.ExitList.Count {2}, LBL_File.HighwayList.Count {3})",
                                       data.ExitHighwayIndex,
                                       data.ExitOffset,
                                       lbl.ExitList.Count,
                                       lbl.HighwayWithExitList.Count);

               } else if (data.ExitOffset != 0xFFFF) {
                  ExitOffset = data.ExitOffset;

                  Debug.WriteLine("ExitHighwayIndex {0}, ExitOffset {1} (LBL_File.ExitList.Count {2}, LBL_File.HighwayList.Count {3})",
                                    data.ExitHighwayIndex,
                                    data.ExitOffset,
                                    lbl.ExitList.Count,
                                    lbl.HighwayWithExitList.Count);

               } else
                  Debug.WriteLine("LblPoiData.ExitIsSet, aber wie?");
            }

         }

         public PoiDataExt(PoiDataExt pd) {
            Text = pd.Text;
            Country = pd.Country;
            Region = pd.Region;
            City = pd.City;
            Zip = pd.Zip;
            Street = pd.Street;
            StreetNumber = pd.StreetNumber;
            PhoneNumber = pd.PhoneNumber;
            ExitHighway = pd.ExitHighway;
            ExitOffset = pd.ExitOffset;
            ExitIndex = pd.ExitIndex;
         }

         public override string ToString() {
            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Text)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Text [{0}]", Text));
            }

            if (!string.IsNullOrEmpty(Country)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Country [{0}]", Country));
            }

            if (!string.IsNullOrEmpty(Region)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Region [{0}]", Region));
            }

            if (!string.IsNullOrEmpty(City)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("City [{0}]", City));
            }

            if (!string.IsNullOrEmpty(Zip)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Zip [{0}]", Zip));
            }

            if (!string.IsNullOrEmpty(Street)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Street [{0}]", Street));
            }

            if (!string.IsNullOrEmpty(StreetNumber)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("StreetNo [{0}]", StreetNumber));
            }

            if (!string.IsNullOrEmpty(PhoneNumber)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Phone [{0}]", PhoneNumber));
            }

            if (!string.IsNullOrEmpty(ExitHighway)) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("ExitHighway [{0}]", ExitHighway));
            }

            if (ExitOffset >= 0) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("ExitOffset [{0}]", ExitOffset));
            }

            if (ExitIndex >= 0) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("ExitIndex [{0}]", ExitIndex));
            }

            return sb.ToString();
         }

      }

      /// <summary>
      /// zusätzliche Daten für normale Straßen (aus der NET-Datei)
      /// </summary>
      public class RoadDataExt {

         public string Country;
         public string Region;
         public string City;
         public string Zip;
         public List<string> Street;
         public uint RoadLength;

         public RoadDataExt(StdFile_NET.RoadData rd, StdFile_LBL lbl) {
            Country = Region = City = Zip = "";
            Street = new List<string>();

            if (rd.ZipIndex > 0)
               Zip = lbl.GetText(lbl.ZipDataList[rd.ZipIndex - 1].TextOffset);

            if (rd.CityIndex > 0) {
               StdFile_LBL.CityAndRegionOrCountryRecord cr = lbl.CityAndRegionOrCountryDataList[rd.CityIndex - 1];

               if (!cr.IsPOI)
                  City = lbl.GetText(cr.TextOffset);
               else {

                  if (cr.POIIndex != 0 && cr.SubdivisionNumber != 0)
                     Debug.WriteLine("POIIndex {0}, SubdivisionNumber {1}",
                                    cr.POIIndex,
                                    cr.SubdivisionNumber);

               }

               if (cr.RegionIsCountry) {
                  Country = lbl.GetText(lbl.CountryDataList[cr.RegionOrCountryIndex - 1].TextOffset);
               } else {
                  Region = lbl.GetText(lbl.RegionAndCountryDataList[cr.RegionOrCountryIndex - 1].TextOffset);
                  Country = lbl.GetText(lbl.CountryDataList[lbl.RegionAndCountryDataList[cr.RegionOrCountryIndex - 1].CountryIndex - 1].TextOffset);
               }
            }

            for (int i = 0; i < rd.LabelInfo.Count; i++)
               Street.Add(lbl.GetText(rd.LabelInfo[i]));

            RoadLength = rd.RoadLength * 2;

         }

         public RoadDataExt(RoadDataExt rd) {
            Country = rd.Country;
            Region = rd.Region;
            City = rd.City;
            Zip = rd.Zip;
            Street = new List<string>();
            foreach (string street in rd.Street)
               Street.Add(street);
            RoadLength = rd.RoadLength;
         }

         public override string ToString() {
            StringBuilder sb = new StringBuilder();

            if (Country.Length > 0) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Country [{0}]", Country));
            }

            if (Region.Length > 0) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Region [{0}]", Region));
            }

            if (City.Length > 0) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("City [{0}]", City));
            }

            if (Zip.Length > 0) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Zip [{0}]", Zip));
            }

            for (int i = 0; i < Street.Count; i++) {
               if (sb.Length > 0)
                  sb.Append(", ");
               sb.Append(string.Format("Street [{0}]", Street[i]));
            }

            return sb.ToString();
         }

      }


      #region Definition der geografischen Objekte; Koordinaten intern immer auf Basis der Mapunits

      public abstract class GraphicObject {

         int _type;

         /// <summary>
         /// vollständiger Typ (erweitert, Haupt- und Subtyp bilden eine max. 5stellige Hex-Zahl)
         /// </summary>
         public int Type {
            get {
               return _type;
            }
            set {
               _type = TypeWithLimitation(value);
            }
         }

         /// <summary>
         /// Einschränkung auf erlaubte Typen
         /// </summary>
         /// <param name="type"></param>
         /// <returns></returns>
         protected int TypeWithLimitation(int type) {
            return type & 0x1FF1F;
         }

         /// <summary>
         /// Erweiterter Typ?
         /// </summary>
         public bool IsExtendedType {
            get {
               return (_type & 0x10000) != 0;
            }
         }

         /// <summary>
         /// Haupttyp 0x00..0xFF
         /// </summary>
         public int MainType {
            get {
               return (_type & 0xFF00) >> 8;
            }
         }

         /// <summary>
         /// Subtyp 0x00..0xFF
         /// </summary>
         public int SubType {
            get {
               return _type & 0xFF;
            }
         }

         /// <summary>
         /// Text des Objektes
         /// </summary>
         public string Label { get; set; }

         protected byte[] _GarminExtraData;

         /// <summary>
         /// Extra-Bytes für erweiterte Punkte (Codierung siehe Garmin)
         /// </summary>
         public byte[] GarminExtraData {
            get {
               return IsExtendedType ? _GarminExtraData : null;
            }
            set {
               if (IsExtendedType) {
                  if (value == null)
                     _GarminExtraData = null;
                  else {
                     if (_GarminExtraData != null &&
                         _GarminExtraData.Length != value.Length)
                        _GarminExtraData = new byte[value.Length];
                     value.CopyTo(_GarminExtraData, 0);
                  }
               }
            }
         }


         public GraphicObject(int type = 0) {
            Type = type;
            Label = null;
         }

         public GraphicObject(GraphicObject go) {
            _type = go._type;
            Label = go.Label;
            if (go.GarminExtraData != null) {
               _GarminExtraData = new byte[go.GarminExtraData.Length];
               go.GarminExtraData.CopyTo(_GarminExtraData, 0);
            }
         }

         public override string ToString() {
            return string.Format("0x{0:x}", Type) + (!string.IsNullOrEmpty(Label) ? ", " + Label : "");
         }
      }

      public class Point : GraphicObject {

         /// <summary>
         /// die Koordinaten
         /// </summary>
         public MapUnitPoint Coordinates;

         /// <summary>
         /// zusätzliche Daten (aus der LBL-Datei); nur für "normale" Typen berücksichtigt
         /// </summary>
         public PoiDataExt LblData;

         /// <summary>
         /// Einschränkung auf erlaubte Typen
         /// </summary>
         /// <param name="type"></param>
         /// <returns></returns>
         protected new int TypeWithLimitation(int type) {
            return (type & 0x10000) != 0 ?
                           type & 0x1FF1F :
                           type & 0x07F1F;
         }


         /// <summary>
         /// erzeugt einen Punkt mit den MapUnit-Angaben
         /// </summary>
         /// <param name="type"></param>
         /// <param name="pt"></param>
         public Point(int type, MapUnitPoint pt)
            : base(type) {
            _GarminExtraData = null;
            Coordinates = new MapUnitPoint(pt);
            LblData = null;
         }

         /// <summary>
         /// erzeugt einen Punkt mit den Gradangaben
         /// </summary>
         /// <param name="type"></param>
         /// <param name="lon"></param>
         /// <param name="lat"></param>
         public Point(int type, double lon = 0, double lat = 0)
            : this(type, new MapUnitPoint(lon, lat)) { }

         /// <summary>
         /// erzeugt einen Punkt aus den gespeicherten Angaben
         /// </summary>
         /// <param name="pt"></param>
         /// <param name="center"></param>
         /// <param name="coordbits"></param>
         public Point(StdFile_RGN.RawPointData pt, MapUnitPoint center, int coordbits) :
            this((pt.Type << 8) | pt.Subtype,
                 new MapUnitPoint(center.Longitude + Coord.RawUnits2MapUnits(pt.RawDeltaLongitude, coordbits),
                                  center.Latitude + Coord.RawUnits2MapUnits(pt.RawDeltaLatitude, coordbits))) { }

         /// <summary>
         /// erzeugt einen Punkt aus den gespeicherten Angaben
         /// </summary>
         /// <param name="pt"></param>
         /// <param name="center"></param>
         /// <param name="coordbits"></param>
         public Point(StdFile_RGN.ExtRawPointData pt, MapUnitPoint center, int coordbits) :
            this(((0x100 | pt.Type) << 8) | pt.Subtype,
                 new MapUnitPoint(center.Longitude + Coord.RawUnits2MapUnits(pt.RawDeltaLongitude, coordbits),
                                  center.Latitude + Coord.RawUnits2MapUnits(pt.RawDeltaLatitude, coordbits))) { }

         public Point(Point p)
            : base(p) {
            Coordinates = new MapUnitPoint(p.Coordinates);
            if (p.LblData != null)
               LblData = new PoiDataExt(p.LblData);
         }


         /// <summary>
         /// liefert die Breite für die RGN-Datei in RawUnits
         /// </summary>
         /// <param name="subdivcenter">Breite der Mitte der Subdiv</param>
         /// <param name="coordbits">Bitanzahl für die Koordinaten</param>
         /// <returns></returns>
         public short Latitude4Save(int subdivcenter, int coordbits) {
            return (short)Coord.MapUnits2RawUnits(Coordinates.Latitude - subdivcenter, coordbits);
         }

         /// <summary>
         /// liefert die Länge für die RGN-Datei in RawUnits
         /// </summary>
         /// <param name="subdivcenter">Länge der Mitte der Subdiv</param>
         /// <param name="coordbits">Bitanzahl für die Koordinaten</param>
         /// <returns></returns>
         public short Longitude4Save(int subdivcenter, int coordbits) {
            return (short)Coord.MapUnits2RawUnits(Coordinates.Longitude - subdivcenter, coordbits);
         }

         public override string ToString() {
            return base.ToString() + string.Format(", Lon={0}, Lat={1} / {2}" + (LblData != null ? ", " + LblData.ToString() : ""),
                                                   Coordinates.LongitudeDegree,
                                                   Coordinates.LatitudeDegree,
                                                   Coordinates.ToString());
         }

      }

      public class Poly : GraphicObject {

         /// <summary>
         /// Daten für Fläche (oder Polylinie)
         /// </summary>
         public bool IsArea { get; private set; }

         /// <summary>
         /// nur für normale Polylinie sinnvoll
         /// </summary>
         public bool DirectionIndicator { get; private set; }

         /// <summary>
         /// Punkte der Punktliste
         /// </summary>
         public class PolyPoint : MapUnitPoint {

            public bool ExtraBit;

            /// <summary>
            /// erzeugt einen Punkt mit den MapUnit-Angaben
            /// </summary>
            /// <param name="lon"></param>
            /// <param name="lat"></param>
            /// <param name="extrabit"></param>
            public PolyPoint(int lon = 0, int lat = 0, bool extrabit = false) {
               ExtraBit = extrabit;
               Latitude = lat;
               Longitude = lon;
            }

            /// <summary>
            /// erzeugt einen Punkt mit den Gradangaben
            /// </summary>
            /// <param name="lon"></param>
            /// <param name="lat"></param>
            /// <param name="extrabit"></param>
            public PolyPoint(double lon, double lat, bool extrabit = false) {
               ExtraBit = extrabit;
               LatitudeDegree = lat;
               LongitudeDegree = lon;
            }

            /// <summary>
            /// erzeugt einen Punkt aus einem <see cref="MapUnitPoint"/>
            /// </summary>
            /// <param name="ptn"></param>
            /// <param name="extrabit"></param>
            public PolyPoint(MapUnitPoint pt, bool extrabit = false) {
               ExtraBit = extrabit;
               Latitude = pt.Latitude;
               Longitude = pt.Longitude;
            }

            /// <summary>
            /// erzeugt eine Punktkopie
            /// </summary>
            /// <param name="pp"></param>
            public PolyPoint(PolyPoint pp) :
               this(pp.Longitude, pp.Latitude, pp.ExtraBit) {
               ExtraBit = pp.ExtraBit;
            }

            public override string ToString() {
               return base.ToString() + string.Format(", ExtraBit={0}", ExtraBit);
            }

         }

         List<PolyPoint> _pt;

         /// <summary>
         /// liefert die Anzahl der Punkte in der Liste
         /// </summary>
         public int PointCount { get { return _pt.Count; } }

         /// <summary>
         /// Einschränkung auf erlaubte Typen
         /// </summary>
         /// <param name="type"></param>
         /// <returns></returns>
         protected new int TypeWithLimitation(int type) {
            return (type & 0x10000) != 0 ?
                           type & 0x1FF1F :
                           (IsArea ? type & 0x07F00 : type & 0x03F00);
         }

         /// <summary>
         /// zusätzliche Daten für "normale" Straßen (aus der NET-Datei)
         /// </summary>
         public RoadDataExt NetData;

         public Bound Bound { get; private set; }


         /// <summary>
         /// erzeugt ein neues Objekt ohne Punktdaten
         /// </summary>
         /// <param name="type"></param>
         /// <param name="directionindicator"></param>
         /// <param name="isarea">true für Fläche, false für Linie</param>
         public Poly(int type, bool directionindicator = false, bool isarea = false)
            : base(type) {
            IsArea = isarea;
            DirectionIndicator = directionindicator;
            _GarminExtraData = null;
            _pt = new List<PolyPoint>();
            NetData = null;
            Bound = null;
         }

         /// <summary>
         /// erzeugt ein Objekt aus den "Rohdaten" der RGN-Datei
         /// </summary>
         /// <param name="poly"></param>
         /// <param name="iLongitudeCenter">Länge des Mittelpunkts der Subdiv in MapUnits</param>
         /// <param name="iLatitudeCenter">Breite des Mittelpunkts der Subdiv in MapUnits</param>
         /// <param name="coordbits">Bits je Koordinate</param>
         public Poly(StdFile_RGN.RawPolyData poly, MapUnitPoint subdiv_center, int coordbits) :
            this((poly.Type << 8) | poly.Subtype, poly.DirectionIndicator, poly.IsPolygon) {
            List<MapUnitPoint> pt = poly.GetMapUnitPoints(coordbits, subdiv_center);
            Debug.WriteLineIf(poly.IsPolygon ? pt.Count < 3 : pt.Count < 2,
                              string.Format("error: polygon/polyline {0} with only {1} points", poly, pt.Count));
            for (int j = 0; j < pt.Count; j++)
               AddPoint(pt[j].LongitudeDegree,
                        pt[j].LatitudeDegree,
                        poly.ExtraBit != null && poly.ExtraBit.Count > j ? poly.ExtraBit[j] : false);
         }

         /// <summary>
         /// erzeugt ein Objekt aus den "Rohdaten" der RGN-Datei
         /// </summary>
         /// <param name="poly"></param>
         /// <param name="iLongitudeCenter">Länge des Mittelpunkts der Subdiv in MapUnits</param>
         /// <param name="iLatitudeCenter">Breite des Mittelpunkts der Subdiv in MapUnits</param>
         /// <param name="coordbits">Bits je Koordinate</param>
         /// <param name="isarea">true für Fläche, false für Linie</param>
         public Poly(StdFile_RGN.ExtRawPolyData poly, MapUnitPoint subdiv_center, int coordbits, bool isarea) :
            this(((0x100 | poly.Type) << 8) | poly.Subtype, false, isarea) {
            List<MapUnitPoint> pt = poly.GetMapUnitPoints(coordbits, subdiv_center);
            Debug.WriteLineIf(pt.Count < 3, string.Format("Fehler: Polygon {0} hat nur {1} Punkte", poly, pt.Count));
            Debug.WriteLineIf(pt.Count < 3, string.Format("Fehler: Polygon {0} hat nur {1} Punkte", poly, pt.Count));
            for (int j = 0; j < pt.Count; j++)
               AddPoint(pt[j].LongitudeDegree,
                        pt[j].LatitudeDegree,
                        false);
            if (poly.HasExtraBytes)
               GarminExtraData = poly.ExtraBytes;
         }

         /// <summary>
         /// erzeugt die Kopie des Objektes
         /// </summary>
         /// <param name="p"></param>
         public Poly(Poly p)
            : base(p) {
            IsArea = p.IsArea;
            DirectionIndicator = p.DirectionIndicator;
            if (p.NetData != null)
               NetData = new RoadDataExt(p.NetData);
            _pt = new List<PolyPoint>();
            foreach (PolyPoint pp in p._pt)
               _pt.Add(new PolyPoint(pp));
            Bound = new Bound(p.Bound);
         }


         /// <summary>
         /// löscht die Punktliste
         /// </summary>
         public void Clear() {
            _pt.Clear();
            Bound = null;
         }

         /// <summary>
         /// entfernt den Punkt aus der Liste
         /// <para>Die Umgrenzung muss danach mit <see cref="CalculateBound"/>() neu berechnet werden!</para>
         /// </summary>
         /// <param name="idx"></param>
         /// <param name="boundcalculate">wenn true, wird <see cref="Bound"/> mit <see cref="CalculateBound"/>() neu berechnet</param>
         public void RemovePoint(int idx, bool boundcalculate = true) {
            if (0 <= idx && idx < _pt.Count) {
               _pt.RemoveAt(idx);
               if (boundcalculate)
                  CalculateBound();
            }
         }

         /// <summary>
         /// entfernt einen Bereich von Punkten
         /// </summary>
         /// <param name="idx">Index des 1. zu entfernenden Punktes</param>
         /// <param name="count">Anzahl der zu entfernenden Punkte</param>
         /// <param name="boundcalculate">wenn true, wird <see cref="Bound"/> mit <see cref="CalculateBound"/>() neu berechnet</param>
         public void RemoveRangeOfPoints(int idx, int count, bool boundcalculate = true) {
            if (count > 0 && 0 <= idx && idx + count <= _pt.Count) {
               _pt.RemoveRange(idx, count);
               if (boundcalculate)
                  CalculateBound();
            }
         }

         /// <summary>
         /// fügt den Punkt an der Position hinzu
         /// </summary>
         /// <param name="lon"></param>
         /// <param name="lat"></param>
         /// <param name="extrabit"></param>
         /// <param name="boundcalculate">wenn true, wird <see cref="Bound"/> mit <see cref="CalculateBound"/>() neu berechnet</param>
         /// <param name="pos"></param>
         public void AddPoint(int lon, int lat, bool extrabit = false, bool boundcalculate = true, int pos = int.MaxValue) {
            AddPoint(new PolyPoint(lon, lat, extrabit), boundcalculate, pos);
         }

         /// <summary>
         /// fügt den Punkt an der Position hinzu
         /// </summary>
         /// <param name="lon"></param>
         /// <param name="lat"></param>
         /// <param name="extrabit"></param>
         /// <param name="boundcalculate">wenn true, wird <see cref="Bound"/> mit <see cref="CalculateBound"/>() neu berechnet</param>
         /// <param name="pos"></param>
         public void AddPoint(double lon, double lat, bool extrabit = false, bool boundcalculate = true, int pos = int.MaxValue) {
            AddPoint(new PolyPoint(lon, lat, extrabit), boundcalculate, pos);
         }

         /// <summary>
         /// fügt den Punkt an der Position hinzu
         /// </summary>
         /// <param name="pt"></param>
         /// <param name="extrabit"></param>
         /// <param name="boundcalculate">wenn true, wird <see cref="Bound"/> mit <see cref="CalculateBound"/>() neu berechnet</param>
         /// <param name="pos"></param>
         public void AddPoint(MapUnitPoint pt, bool extrabit = false, bool boundcalculate = true, int pos = int.MaxValue) {
            AddPoint(new PolyPoint(pt, extrabit), boundcalculate, pos);
         }

         /// <summary>
         /// fügt den Punkt an der Position hinzu
         /// </summary>
         /// <param name="pt"></param>
         /// <param name="boundcalculate">wenn true, wird <see cref="Bound"/> mit <see cref="CalculateBound"/>() neu berechnet</param>
         /// <param name="pos"></param>
         public void AddPoint(PolyPoint pt, bool boundcalculate = true, int pos = int.MaxValue) {
            if (pos < 0)
               pos = 0;
            if (pos >= _pt.Count)
               _pt.Add(pt);
            else
               _pt.Insert(pos, pt);

            if (boundcalculate)
               if (Bound == null)
                  Bound = new Bound(pt);
               else
                  Bound.Embed(pt.Longitude, pt.Latitude);
         }

         /// <summary>
         /// liefert den Punkt als Referenz (oder null)
         /// <para>Das ist impliziet auch eine <see cref="MapUnitPoint"/>.</para>
         /// </summary>
         /// <param name="idx"></param>
         /// <returns></returns>
         public PolyPoint GetPoint(int idx) {
            return 0 <= idx && idx < _pt.Count ?
                        _pt[idx] :
                        null;
         }

         /// <summary>
         /// liefert die gesamte Punktliste als <see cref="MapUnitPoint"/>-Liste (Kopie der internen Liste)
         /// </summary>
         /// <returns></returns>
         public List<MapUnitPoint> GetMapUnitPoints() {
            List<MapUnitPoint> lst = new List<MapUnitPoint>();
            foreach (var item in _pt)
               lst.Add(new MapUnitPoint(item));
            return lst;
         }

         /// <summary>
         /// berechnet das umschließende "Rechteck" in MapUnits neu und liefert eine Kopie der <see cref="Bound"/>
         /// </summary>
         /// <returns></returns>
         public Bound CalculateBound() {
            switch (_pt.Count) {
               case 0:
                  Bound = null;
                  return null;

               case 1:
                  Bound = new Bound(_pt[0]);
                  break;

               default:
                  Bound = new Bound(GetMapUnitPoints());
                  break;
            }
            return new Bound(Bound);
         }

         /// <summary>
         /// erzeugt ein <see cref="StdFile_RGN.RawPolyData"/>-Objekt für die Speicherung
         /// </summary>
         /// <param name="latitudecenter">Breite in Mapunits</param>
         /// <param name="longitudecenter">Höhe in Mapunits</param>
         /// <param name="coordbits">Bitanzahl für die Koordinaten</param>
         /// <param name="isarea">true für Fläche, false für Linie</param>
         /// <returns></returns>
         public StdFile_RGN.RawPolyData BuildRgnPolyData(MapUnitPoint center, int coordbits, bool isarea) {
            if (IsExtendedType)
               throw new Exception("Funktion ist nicht für erweiterte Objekte verwendbar.");

            StdFile_RGN.RawPolyData polydat = new StdFile_RGN.RawPolyData();
            polydat.IsPolygon = isarea;
            polydat.DirectionIndicator = DirectionIndicator;
            polydat.LabelInNET = false;
            polydat.Type = MainType;

            List<MapUnitPoint> pt = new List<MapUnitPoint>();
            for (int i = 0; i < PointCount; i++) {
               MapUnitPoint p = GetPoint(i);
               if (pt.Count == 0 ||
                   pt[pt.Count - 1] != p)
                  pt.Add(p);
            }
            if (pt.Count == 1)
               pt.Add(new MapUnitPoint(pt[0])); // damit pro forma min. 2 Punkte ex.
            polydat.SetMapUnitPoints(coordbits, center, pt);

            return polydat;
         }

         /// <summary>
         /// erzeugt ein <see cref="StdFile_RGN.ExtRawPolyData"/>-Objekt für die Speicherung
         /// </summary>
         /// <param name="latitudecenter">Breite in Mapunits</param>
         /// <param name="longitudecenter">Höhe in Mapunits</param>
         /// <param name="coordbits">Bitanzahl für die Koordinaten</param>
         /// <returns></returns>
         public StdFile_RGN.ExtRawPolyData BuildRgnExtPolyData(MapUnitPoint center, int coordbits) {
            if (!IsExtendedType)
               throw new Exception("Funktion ist nur für erweiterte Objekte verwendbar.");

            StdFile_RGN.ExtRawPolyData polydat = new StdFile_RGN.ExtRawPolyData();
            polydat.Type = MainType;
            polydat.Subtype = SubType;

            List<MapUnitPoint> pt = new List<MapUnitPoint>();
            for (int i = 0; i < PointCount; i++) {
               MapUnitPoint p = GetPoint(i);
               if (pt.Count == 0 ||
                   pt[pt.Count - 1] != p)
                  pt.Add(p);
            }
            if (pt.Count == 1)
               pt.Add(new MapUnitPoint(pt[0])); // damit pro forma min. 2 Punkte ex.
            polydat.SetMapUnitPoints(coordbits, center, pt);

            return polydat;
         }

         public override string ToString() {
            return base.ToString() + string.Format(", IsPolygon={0}, PointCount={1}, DirectionIndicator={2}, Bound={3}",
                                                   IsArea,
                                                   PointCount,
                                                   DirectionIndicator,
                                                   Bound);
         }

         public string ToStringExt() {
            StringBuilder sb = new StringBuilder(ToString());
            sb.Append(", Pointlist:");
            for (int i = 0; i < PointCount; i++)
               sb.AppendFormat(" ({0})", GetPoint(i).ToString());
            return sb.ToString();
         }

      }

      #endregion


      /// <summary>
      /// Liste aller Punkte ("normale" und erweiterte Typen)
      /// </summary>
      public List<Point> PointList { get; private set; }

      /// <summary>
      /// Liste aller Gebiete ("normale" und erweiterte Typen)
      /// </summary>
      public List<Poly> AreaList { get; private set; }

      /// <summary>
      /// Liste aller Linien ("normale" und erweiterte Typen)
      /// </summary>
      public List<Poly> LineList { get; private set; }


      // Eine ChildMap wird mit einem Parent verlinkt, wenn ihr Parent gesetzt wird. Wird ihr Parent auf null gesetzt, wird die Verlinkung gelöst.
      // Aus der Sicht des Parent wird ein Child i mit "parent.GetChildMap(i).ParentMap = null" gelöst.

      DetailMap _ParentMap;

      /// <summary>
      /// liefert die übergeordnete Karte oder setzt sie
      /// <para>Wird die übergeordnete Karte gesetzt, wird sie aktuelle Karte in deren <see cref="ChildMaps"/> aufgenommen.</para>
      /// <para>Wird null gesetzt, wird die Verbindung zur übergeordnete Karte getrennt.</para>
      /// </summary>
      public DetailMap ParentMap {
         get {
            return _ParentMap;
         }
         set {
            if (value != null) {
               if (_ParentMap != null) // dann erst lösen
                  ParentMap = null;
               _ParentMap = value;
               if (!ParentMap.ChildMaps.Contains(this))
                  ParentMap.ChildMaps.Add(this);
            } else {
               if (_ParentMap != null) {
                  if (ParentMap.ChildMaps.Contains(this))
                     ParentMap.ChildMaps.Remove(this);
                  _ParentMap = null;
               }
            }
         }
      }

      /// <summary>
      /// Liste der untergeordneten Karten
      /// </summary>
      List<DetailMap> ChildMaps; // { get; private set; }

      /// <summary>
      /// liefert die Anzahl der untergeordneten Karten
      /// </summary>
      public int ChildMapCount {
         get {
            return ChildMaps != null ? ChildMaps.Count : 0;
         }
      }

      /// <summary>
      /// liefert eine bestimmte untergeordnete Karte
      /// </summary>
      /// <param name="i"></param>
      /// <returns></returns>
      public DetailMap GetChildMap(int i) {
         return 0 <= i && i < ChildMapCount ? ChildMaps[i] : null;
      }

      /// <summary>
      /// liefert den Index der ChildMap oder einen negativen Wert, wenn sie nicht verlinkt ist
      /// </summary>
      /// <param name="childmap"></param>
      /// <returns></returns>
      public int Idx4ChildMap(DetailMap childmap) {
         if (ChildMaps != null)
            return ChildMaps.IndexOf(childmap);
         return -1;
      }

      /// <summary>
      /// gewünschte Grenzen der Karte in Grad
      /// </summary>
      public Bound DesiredBounds { get; set; }

      /// <summary>
      /// Enthält die Karte irgendwelche Objekte?
      /// </summary>
      public bool IsEmpty {
         get {
            return AreaList.Count == 0 && LineList.Count == 0 && PointList.Count == 0;
         }
      }

      /// <summary>
      /// berechnet die aktuellen Grenzen der Karte aus den Kartenobjekten (null, wenn keine Objekte vorhanden sind)
      /// </summary>
      /// <returns></returns>
      public Bound CalculateBounds() {
         if (PointList.Count == 0 &&
             AreaList.Count == 0 &&
             LineList.Count == 0)
            return null;

         Bound b = null;
         // irgendein gültiges Anfangs-Bound erzeugen
         if (AreaList.Count > 0)
            b = new Bound(AreaList[0].Bound);
         else if (LineList.Count > 0)
            b = new Bound(LineList[0].Bound);
         else
            b = new Bound(PointList[0].Coordinates);

         for (int i = 0; i < AreaList.Count; i++)
            b.Embed(AreaList[i].Bound);
         for (int i = 0; i < LineList.Count; i++)
            b.Embed(LineList[i].Bound);
         for (int i = 0; i < PointList.Count; i++)
            b.Embed(PointList[i].Coordinates, true);

         return b;
      }


      public DetailMap(DetailMap parent = null, Bound desiredbounds = null) {
         ParentMap = parent;
         ChildMaps = new List<DetailMap>();

         PointList = new List<Point>();
         AreaList = new List<Poly>();
         LineList = new List<Poly>();

         if (desiredbounds == null)
            DesiredBounds = new Bound();
         else
            DesiredBounds = new Bound(desiredbounds);
      }

      /// <summary>
      /// erzeugt eine echte Kopie der Karte mit kopierten Daten und mit dem gleichen Parent
      /// </summary>
      /// <param name="bounds">wenn ungleich null werden Punkte und Flächen/Linien die nicht vollständig bzw. teilweise in Bounds liegen nicht übernommen</param>
      /// <param name="fullenclosed">wenn true, müssen Punkte und Flächen/Linien für die Übernahme vollständig innerhalb der Bounds liegen</param>
      /// <param name="pointtypes">wenn die Typ-Liste ungleich null ist, werden nur die aufgelisteten Typen übernommen, sonst alle</param>
      /// <param name="areatypes">wenn die Typ-Liste ungleich null ist, werden nur die aufgelisteten Typen übernommen, sonst alle</param>
      /// <param name="linetypes">wenn die Typ-Liste ungleich null ist, werden nur die aufgelisteten Typen übernommen, sonst alle</param>
      /// <returns></returns>
      public DetailMap Copy(Bound bounds = null, bool fullenclosed = true, SortedSet<int> pointtypes = null, SortedSet<int> linetypes = null, SortedSet<int> areatypes = null) {
         DetailMap copy = new DetailMap(ParentMap, bounds == null ? DesiredBounds : bounds);

         for (int i = 0; i < ChildMaps.Count; i++)
            copy.ChildMaps.Add(ChildMaps[i]);

         for (int i = 0; i < PointList.Count; i++)
            if (pointtypes == null || pointtypes.Contains(PointList[i].Type))
               if (bounds == null || bounds.IsEnclosed(PointList[i].Coordinates))
                  copy.PointList.Add(new Point(PointList[i]));

         for (int i = 0; i < AreaList.Count; i++)
            if (areatypes == null || areatypes.Contains(AreaList[i].Type)) {
               Bound bound = bounds != null ? AreaList[i].Bound : null;
               if (bounds == null ||
                   (!fullenclosed && bounds.Intersection(bound) != null) ||
                   (fullenclosed && bounds.IsEnclosed(bound)))
                  copy.AreaList.Add(new Poly(AreaList[i]));
            }

         for (int i = 0; i < LineList.Count; i++)
            if (linetypes == null || linetypes.Contains(LineList[i].Type)) {
               Bound bound = bounds != null ? LineList[i].Bound : null;
               if (bounds == null ||
                   (!fullenclosed && bounds.Intersection(bound) != null) ||
                   (fullenclosed && bounds.IsEnclosed(bound)))
                  copy.LineList.Add(new Poly(LineList[i]));
            }

         return copy;
      }

      /// <summary>
      /// liefert die Anzahl der Parent-Karten (übergeordnete Ebenen)
      /// </summary>
      /// <returns></returns>
      public int Parents() {
         if (ParentMap != null)
            return ParentMap.Parents() + 1;
         return 0;
      }

      /// <summary>
      /// ermittelt die Anzahl der untergeordneten Karten-Ebenen + der eigenen Ebene (also 1..)
      /// </summary>
      /// <returns></returns>
      public int Levels() {
         int max = 0;
         for (int i = 0; i < ChildMaps.Count; i++)
            max = Math.Max(max, ChildMaps[i].Levels());
         return max + 1;
      }

      /// <summary>
      /// liefert die Anzahl der Punkte
      /// <para>Die Gesamtanzahl kann direkt aus der Größe der zugehörigen Liste ermittelt werden.</para>
      /// </summary>
      /// <param name="ext">wenn true, dann nur erweiterte Typen, sonst nur normale</param>
      /// <returns></returns>
      public int PointCount(bool ext = false) {
         int count = 0;
         foreach (Point point in PointList)
            if (!point.IsExtendedType)
               count++;
         return ext ? PointList.Count - count : count;
      }

      /// <summary>
      /// liefert die Anzahl der Linien
      /// <para>Die Gesamtanzahl kann direkt aus der Größe der zugehörigen Liste ermittelt werden.</para>
      /// </summary>
      /// <param name="ext">wenn true, dann nur erweiterte Typen, sonst nur normale</param>
      /// <returns></returns>
      public int LineCount(bool ext = false) {
         int count = 0;
         foreach (Poly poly in LineList)
            if (!poly.IsExtendedType)
               count++;
         return ext ? LineList.Count - count : count;
      }

      /// <summary>
      /// liefert die Anzahl der Flächen
      /// <para>Die Gesamtanzahl kann direkt aus der Größe der zugehörigen Liste ermittelt werden.</para>
      /// </summary>
      /// <param name="ext">wenn true, dann nur erweiterte Typen, sonst nur normale</param>
      /// <returns></returns>
      public int AreaCount(bool ext = false) {
         int count = 0;
         foreach (Poly poly in AreaList)
            if (!poly.IsExtendedType)
               count++;
         return ext ? AreaList.Count - count : count;
      }

      /// <summary>
      /// liefert alle vorhandenen Typen (sortiert)
      /// </summary>
      /// <param name="ext">wenn true, dann nur erweiterte Typen, sonst nur normale</param>
      /// <returns></returns>
      public int[] GetAreaTypes(bool ext = false) {
         SortedSet<int> types = new SortedSet<int>();
         foreach (Poly poly in AreaList) {
            if (ext) {
               if (poly.Type > 0xFFFF)
                  if (!types.Contains(poly.Type))
                     types.Add(poly.Type);
            } else {
               if (poly.Type < 0x10000)
                  if (!types.Contains(poly.Type))
                     types.Add(poly.Type);
            }
         }
         int[] t = new int[types.Count];
         types.CopyTo(t);
         return t;
      }

      /// <summary>
      /// liefert alle vorhandenen Typen (sortiert)
      /// </summary>
      /// <param name="ext">wenn true, dann nur erweiterte Typen, sonst nur normale</param>
      /// <returns></returns>
      public int[] GetLineTypes(bool ext = false) {
         SortedSet<int> types = new SortedSet<int>();
         foreach (Poly poly in LineList) {
            if (ext) {
               if (poly.Type > 0xFFFF)
                  if (!types.Contains(poly.Type))
                     types.Add(poly.Type);
            } else {
               if (poly.Type < 0x10000)
                  if (!types.Contains(poly.Type))
                     types.Add(poly.Type);
            }
         }
         int[] t = new int[types.Count];
         types.CopyTo(t);
         return t;
      }

      /// <summary>
      /// liefert alle vorhandenen Typen (sortiert)
      /// </summary>
      /// <param name="ext">wenn true, dann nur erweiterte Typen, sonst nur normale</param>
      /// <returns></returns>
      public int[] GetPointTypes(bool ext = false) {
         SortedSet<int> types = new SortedSet<int>();
         foreach (Point point in PointList) {
            if (ext) {
               if (point.Type > 0xFFFF)
                  if (!types.Contains(point.Type))
                     types.Add(point.Type);
            } else {
               if (point.Type < 0x10000)
                  if (!types.Contains(point.Type))
                     types.Add(point.Type);
            }
         }
         int[] t = new int[types.Count];
         types.CopyTo(t);
         return t;
      }

      /// <summary>
      /// liefert eine Liste aller Objekte mit diesem (vollständigen) Typ
      /// <para>Achtung: Es wird nur eine Referenz auf die Objekte geliefert, keine Kopie.</para>
      /// </summary>
      /// <param name="type"></param>
      /// <returns></returns>
      public List<Point> PointList4Typ(int type) {
         List<Point> lst = new List<Point>();
         foreach (Point pt in PointList)
            if (pt.Type == type)
               lst.Add(pt);
         return lst;
      }

      /// <summary>
      /// liefert eine Liste aller Objekte mit diesem (vollständigen) Typ
      /// <para>Achtung: Es wird nur eine Referenz auf die Objekte geliefert, keine Kopie.</para>
      /// </summary>
      /// <param name="type"></param>
      /// <returns></returns>
      public List<Poly> LineList4Typ(int type) {
         List<Poly> lst = new List<Poly>();
         foreach (Poly poly in LineList)
            if (poly.Type == type)
               lst.Add(poly);
         return lst;
      }

      /// <summary>
      /// liefert eine Liste aller Objekte mit diesem (vollständigen) Typ
      /// <para>Achtung: Es wird nur eine Referenz auf die Objekte geliefert, keine Kopie.</para>
      /// </summary>
      /// <param name="type"></param>
      /// <returns></returns>
      public List<Poly> AreaList4Typ(int type) {
         List<Poly> lst = new List<Poly>();
         foreach (Poly poly in AreaList)
            if (poly.Type == type)
               lst.Add(poly);
         return lst;
      }

      /// <summary>
      /// behält nur Objekte mit Typen aus der jeweiligen Liste
      /// </summary>
      /// <param name="pointtypes"></param>
      /// <param name="linetypes"></param>
      /// <param name="areatypes"></param>
      public void KeepTypes(SortedSet<int> pointtypes, SortedSet<int> linetypes, SortedSet<int> areatypes) {
         if (pointtypes != null) {
            for (int i = PointList.Count - 1; i >= 0; i--)
               if (!pointtypes.Contains(PointList[i].Type))
                  PointList.RemoveAt(i);
         } else
            PointList.Clear();

         if (areatypes != null) {
            for (int i = AreaList.Count - 1; i >= 0; i--)
               if (!areatypes.Contains(AreaList[i].Type))
                  AreaList.RemoveAt(i);
         } else
            AreaList.Clear();

         if (linetypes != null) {
            for (int i = LineList.Count - 1; i >= 0; i--)
               if (!linetypes.Contains(LineList[i].Type))
                  LineList.RemoveAt(i);
         } else
            LineList.Clear();
      }


      /// <summary>
      /// prüft den untergeordneten Baum der Detailkarten
      /// </summary>
      public void CheckSubtree() {
         if (ChildMaps.Count > 0) {      // sonst Endknoten
            int levels = ChildMaps[0].Levels();
            for (int i = 1; i < ChildMaps.Count; i++)
               if (levels != ChildMaps[0].Levels())
                  throw new Exception("Unterschiedliche Ebenentiefe. (Indexpfad " + GetIndexPathAsString() + ")");
         }
      }

      /// <summary>
      /// liefert die Folge der Indexe im Baum zu dieser Karte als Zeichenkette (praktisch eine "ID")
      /// </summary>
      /// <returns></returns>
      public string GetIndexPathAsString() {
         List<int> idx = GetIndexPath();
         string tmp = "";
         if (idx.Count > 0) {
            tmp = idx[0].ToString();
            for (int i = 1; i < idx.Count; i++)
               tmp += ", " + idx[i].ToString();
         }
         return tmp;
      }

      /// <summary>
      /// liefert die Folge der Indexe im Baum zu dieser Karte (praktisch eine "ID")
      /// </summary>
      /// <returns></returns>
      public List<int> GetIndexPath() {
         List<int> idx = new List<int>();
         DetailMap map = this;
         do {
            if (map.ParentMap != null) {
               for (int i = 0; i < map.ParentMap.ChildMaps.Count; i++)
                  if (map.ParentMap.ChildMaps[i] == map) {
                     idx.Insert(0, i);
                     break;
                  }
            }
            map = map.ParentMap;
         } while (map != null);
         return idx;
      }

      /// <summary>
      /// alle Objekte der Karte werden übernommen (nur als Referenz)
      /// </summary>
      /// <param name="map"></param>
      public void Merge(DetailMap map) {
         PointList.AddRange(map.PointList);
         LineList.AddRange(map.LineList);
         AreaList.AddRange(map.AreaList);
         if (DesiredBounds != null)
            DesiredBounds.Embed(map.DesiredBounds);
         else
            DesiredBounds = new Bound(map.DesiredBounds);
      }


      public override string ToString() {
         return string.Format("PointList {0}, LineList {1}, AreaList {2}, ChildMaps {3}, Parents {4}, DesiredBounds {5}",
                              PointList.Count,
                              LineList.Count,
                              AreaList.Count,
                              ChildMaps.Count,
                              Parents(),
                              DesiredBounds);
      }

   }
}
