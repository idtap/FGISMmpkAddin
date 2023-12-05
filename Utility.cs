using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FGISMmpkAddin
{
    public class Utility
    {
        private static Dictionary<string, List<IDisposable>> _Graphics =
            new Dictionary<string, List<IDisposable>>();

        public static string AddinAssemblyLocation()
        {
            return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public static Polygon GetOutermostRings(Polygon inputPolygon)
        {
            if (inputPolygon == null || inputPolygon.IsEmpty)
              return null;

            List<Polygon> internalRings = new List<Polygon>();

            // explode the parts of the polygon into a list of individual geometries
            // see the "Get the individual parts of a multipart feature"
            // snippet for MultipartToSinglePart
            var parts = GeometryEngine.Instance.MultipartToSinglePart(inputPolygon);

            // get an enumeration of clockwise geometries (area > 0) ordered by the area
            var clockwiseParts = parts.Where(geom => ((Polygon)geom).Area > 0)
                                  .OrderByDescending(geom => ((Polygon)geom).Area);

            // for each of the exterior rings
            foreach (var part in clockwiseParts)
            {
              // add the first (the largest) ring into the internal collection
              if (internalRings.Count == 0)
                internalRings.Add(part as Polygon);

              // use flag to indicate if current part is within the already selection polygons
              bool isWithin = false;

              foreach (var item in internalRings)
              {
                if (GeometryEngine.Instance.Within(part, item))
                  isWithin = true;
              }

              // if the current polygon is not within any polygon of the internal collection
              // then it is disjoint and needs to be added to
              if (isWithin == false)
                internalRings.Add(part as Polygon);
            }

            PolygonBuilderEx outerRings = new PolygonBuilderEx();
            // now assemble a new polygon geometry based on the internal polygon collection
            foreach (var ring in internalRings)
            {
              outerRings.AddParts(ring.Parts);
            }

            // return the final geometry of the outer rings
            return outerRings.ToGeometry();
        }


        // 讀取 json 檔
        public static T LoadFromJsonFile<T>(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {                
                return default(T);
            }
        }

        // json 存檔
        public static void SaveToJsonFile(string filePath, object data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
            }
        }

        // http GET 讀取圖資服務 
        public static string myHttpGET(string api_url, Encoding encode)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(api_url);
            request.Method = "GET";

            var result = "error"; 
            try
            {
                // 發送請求並取得回應
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                // 檢查 HTTP 狀態碼
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // 讀取回應內容
                    using (Stream dataStream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(dataStream, encode))
                    {
                        string responseText = reader.ReadToEnd();
                        result = responseText;
                    }
                }
                else
                {
                }
                response.Close();
            }
            catch (WebException ex)
            {
            }

            return result;
        }

        public static string myHttpPOST(string api_url, string send_data, Encoding encode)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(api_url);
            request.Method = "POST";

            // 附加參數
            byte[] content = Encoding.UTF8.GetBytes(send_data);
            request.ContentLength = content.Length;
            request.ContentType = "application/x-www-form-urlencoded";
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(content, 0, content.Length);
            requestStream.Close();

            var result = "error"; 
            try
            {
                // 發送請求並取得回應
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                // 檢查 HTTP 狀態碼
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // 讀取回應內容
                    using (Stream dataStream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(dataStream, encode))
                    {
                        string responseText = reader.ReadToEnd();
                        result = responseText;
                    }
                }
                response.Close();
            }
            catch (WebException ex)
            {
            }

            return result;
        }

        // 依索引值取得 graphic(無則加入此圖)
        public static List<IDisposable> Graphics(string key)
        {
            var graphic = _Graphics.ContainsKey(key) ?
                _Graphics[key] : new List<IDisposable>();
            if (!_Graphics.ContainsKey(key)) _Graphics.Add(
                 key, graphic);
            return graphic;
        }

        // 取得 graphics
        public static Dictionary<string, List<IDisposable>> GetGraphics()
        {
            return _Graphics;
        }

        // new 紅色星號點 symbol 
        public static Task<CIMPointSymbol> GetStartSymbol()
        {
            return QueuedTask.Run(() =>
            {
                CIMMarker marker = SymbolFactory.Instance.ConstructMarker(
                      ColorFactory.Instance.RedRGB, 10, SimpleMarkerStyle.Star);
                CIMSymbolLayer[] layers = new CIMSymbolLayer[1];
                layers[0] = marker;
                return new CIMPointSymbol()
                {
                    SymbolLayers = layers,
                    ScaleX = 1
                };
            });
        }

        // 由Map Featurelayer service依fid取得features
        // (ps: fids/feature ids, layer/圖層服務網址, lyrid/圖層id )
        public static async Task<IEnumerable<Feature>> GetFeature(List<long> fids, string layer, string lyrid)
        {
            ServiceConnectionProperties Online = new ServiceConnectionProperties(new Uri(layer));
            try
            {
                return await QueuedTask.Run(() =>
                {
                    using (Geodatabase gdb = new Geodatabase(Online))
                    {
                        using (Table tb = gdb.OpenDataset<Table>(lyrid))
                        {
                            List<Feature> list = new List<Feature>();
                            ArcGIS.Core.Data.QueryFilter filter = new ArcGIS.Core.Data.QueryFilter()
                            {
                                ObjectIDs = fids
                            };
                            RowCursor rows = tb.Search(filter);
                            while (rows.MoveNext())
                            {
                                list.Add(rows.Current as Feature);
                            }
                            return list;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        // 由Map featurelayer service依wherecaus條件查詢圖徵
        // (ps:layer/Feturelayer service網址)
        public static async Task<IEnumerable<Feature>> GetFeatures(string layer, string lyrid, string whereClause, string subFields = "*")
        {
            ServiceConnectionProperties Online = new ServiceConnectionProperties(new Uri(layer));
            try
            {
                return await QueuedTask.Run(() =>
                {
                    using (Geodatabase gdb = new Geodatabase(Online))
                    {
                        using (Table tb = gdb.OpenDataset<Table>(lyrid))
                        {

                            List<Feature> list = new List<Feature>();
                            ArcGIS.Core.Data.QueryFilter filter = new ArcGIS.Core.Data.QueryFilter()
                            {
                                WhereClause = whereClause,
                                SubFields = subFields
                            };
                            RowCursor rows = tb.Search(filter,false);
                            int count = 0;
                            while (count<250 && rows.MoveNext())
                            {
                                list.Add(rows.Current as Feature);
                                count++;    

                            }
                            
                            return list;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        
        // 由 service 網址取得 features
        public static async Task<IEnumerable<Feature>> GetFeatures(string layer, string lyrid,
            ArcGIS.Core.Geometry.Geometry geometry, string whereClause, string subFields = "*",
            string User = "", string password = "")
        {
            ServiceConnectionProperties Online = new ServiceConnectionProperties(new Uri(layer))
            {
                User = User,
                Password = password
            };
            try
            {
                return await QueuedTask.Run(() =>
                {
                    using (Geodatabase gdb = new Geodatabase(Online))
                    {
                        using (Table tb = gdb.OpenDataset<Table>(lyrid))
                        {

                            List<Feature> list = new List<Feature>();
                            SpatialQueryFilter filter = new SpatialQueryFilter()
                            {
                                FilterGeometry = geometry,
                                SpatialRelationship = SpatialRelationship.Intersects,
                                WhereClause = whereClause,
                                SubFields = subFields
                            };
                            RowCursor rows = tb.Search(filter);
                            while (rows.MoveNext())
                            {
                                list.Add(rows.Current as Feature);
                            }
                            return list;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
