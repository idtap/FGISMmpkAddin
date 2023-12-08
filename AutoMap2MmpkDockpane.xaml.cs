
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Windows.Shapes;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using Envelope = ArcGIS.Core.Geometry.Envelope;
using Polygon = ArcGIS.Core.Geometry.Polygon;

namespace FGISMmpkAddin
{
    public class MmpkParamModel
    {
        public string MmpkName { get; set; }
        public string PortalUrl { get; set; }
        public string UserID { get; set; }
        public string UserPassword { get; set; }
        public string TempPath { get; set; }
    }

    public partial class AutoMap2MmpkDockpaneView : UserControl
    {
        private static string mmpkParamPath = Utility.AddinAssemblyLocation()+"/Images/mmpkParam.json";
        private static string pathProExe = null;
        private static string pathPython = null;
        private static string clipPolygonName = "ClipPolygon";
        private static string tempRootPath = "D:\\mmpkTemp\\";

        public AutoMap2MmpkDockpaneView()
        {
            InitializeComponent();
            initNeed();
            LoadMmpkParam();
            tempRootPath = txtTempPath.Text.ToString();
        }

        private void initNeed()
        {
            pathProExe = System.IO.Path.GetDirectoryName((new System.Uri(Assembly.GetEntryAssembly().Location)).AbsolutePath);
            if (pathProExe == null) return;
            pathProExe = Uri.UnescapeDataString(pathProExe);
            pathProExe = System.IO.Path.Combine(pathProExe, @"Python\envs\arcgispro-py3");

            pathPython = System.IO.Path.GetDirectoryName((new System.Uri(Assembly.GetExecutingAssembly().Location)).AbsolutePath);
            if (pathPython == null) return;
            pathPython = Uri.UnescapeDataString(pathPython);
        }   
        
        private string RunPy(string pyFileName, string param)
        {
            var myCommand = string.Format(@"/c """"{0}"" ""{1}""""",
                System.IO.Path.Combine(pathProExe, "python.exe"),
                System.IO.Path.Combine(pathPython, pyFileName));

            myCommand += " " + param;
            var procStartInfo = new System.Diagnostics.ProcessStartInfo("cmd", myCommand);
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();

            string result = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error)) result += string.Format("Failed,{0} Error: {1}", result, error);

            return result;
        }

        public void LoadMmpkParam()
        {
            List<MmpkParamModel> tempLists = Utility.LoadFromJsonFile
                                                   <List<MmpkParamModel>>(mmpkParamPath);
            if( tempLists!=null )
            {
                txtMmpkName.Text    =tempLists[0].MmpkName;
                txtPortalUrl.Text   =tempLists[0].PortalUrl;
                txtUserID.Text      =tempLists[0].UserID;
                txtUserPassword.Text=tempLists[0].UserPassword;
                txtTempPath.Text    =tempLists[0].TempPath;
            }
        }

        public void SaveMmpkParam()
        {
            List<MmpkParamModel> tempLists = new List<MmpkParamModel>();
            MmpkParamModel param = new MmpkParamModel()
            {
                MmpkName     =txtMmpkName.Text.ToString(),
                PortalUrl    =txtPortalUrl.Text.ToString(),
                UserID       =txtUserID.Text.ToString(),
                UserPassword =txtUserPassword.Text.ToString(),
                TempPath     =txtTempPath.Text.ToString()
            };
            tempLists.Add(param);
            Utility.SaveToJsonFile(mmpkParamPath, tempLists);
        }
        
        private void CheckAndClearTemp()
        {
            // 預計輸出至 C:\Temp，檢查及清除
            if( Directory.Exists(tempRootPath) == false )
                Directory.CreateDirectory(tempRootPath);
            // 複製空白 .aprx 檔
            var empty_aprx = System.IO.Path.Combine(pathPython, "empty.aprx");
            if (System.IO.File.Exists(tempRootPath+"Temp.aprx"))
                System.IO.File.Delete(tempRootPath+"Temp.aprx");
            System.IO.File.Copy(empty_aprx,tempRootPath+"Temp.aprx");
            // mmpk 也要刪除
            if (System.IO.File.Exists(tempRootPath + "mmpkTemp.mmpk"))
                System.IO.File.Delete(tempRootPath + "mmpkTemp.mmpk");
        }

        private string CreateTPKX( string sour_url, string tpkx_name )
        {
            var tpkx_fullname = tempRootPath+tpkx_name+".tpkx";
            if( System.IO.File.Exists(tpkx_fullname) ) 
                System.IO.File.Delete(tpkx_fullname);

            // 組合參數後執行
            var param = sour_url+" "+tpkx_name;
            var result = RunPy("CreateTPKX.py", param);
            return result;
        }

        private async void getClipRaster(string url, string clip_file, ArcGIS.Core.Geometry.Envelope genExtent)
        {
            string rectangle = genExtent.XMin.ToString()+" "+genExtent.YMin.ToString()+" "+
                               genExtent.XMax.ToString()+" "+genExtent.YMax.ToString();
            var parameters = Geoprocessing.MakeValueArray(url, rectangle, clip_file, null, "256", null, "NO_MAINTAIN_EXTENT");
            var results = await Geoprocessing.ExecuteToolAsync("management.Clip", parameters);
            
        }

        private async void clipLayerToFeatures(string clip_layer)
        {
            var out_file = tempRootPath+clip_layer+".shp";
            var parameters = Geoprocessing.MakeValueArray(clip_layer, "POLYGON", out_file, "KEEP_GRAPHICS");
            var results = await Geoprocessing.ExecuteToolAsync("conversion.GraphicsToFeatures", parameters);
        }


        private async void getClipFeature(string url, string clip_file, string clip_layer)
        {
            var clip_feature = tempRootPath+clip_layer+".shp";
            var parameters = Geoprocessing.MakeValueArray(url, clip_feature, clip_file, null);
            var results = await Geoprocessing.ExecuteToolAsync("analysis.Clip", parameters);
            
        }

        private string CreateVTPK( string shp_file_path, string vtpk_name )
        {
            // 組合參數後執行
            var param = shp_file_path+"  "+vtpk_name;
            var result = RunPy("CreateVTPK.py", param);

            return result;
        }

        private string CreateMMPK( string mmpk_file_path, int vtpk_count, int tpkx_count )
        {
            // 由 count 組合分號區隔 vtpk tpkx file list
            var file_list = "";
            for( var ii=0; ii<vtpk_count; ii++ )
            {
                var vtpk_file = "vtpk_"+(ii+1).ToString()+".vtpk";
                if( !file_list.Equals("") )
                    file_list += ";";
                file_list += tempRootPath+vtpk_file;
            }
            for( var ii=0; ii<tpkx_count; ii++ )
            {
                var tpkx_file = "tpkx_"+(ii+1).ToString()+".tpkx";
                if( !file_list.Equals("") )
                    file_list += ";";
                file_list += tempRootPath+tpkx_file;
            }

            // 組合參數後執行
            var param = mmpk_file_path+"  "+file_list;
            var result = RunPy("CreateMMPK.py", param);

            return result;
        }

        private string UploadMMPK( string mmpk_file_path )
        {
            // 組合參數後執行
            var mmpk_info = mmpk_file_path+";"+txtMmpkName.Text.ToString();

            var url_id_pass = txtPortalUrl.Text.ToString()+";"+
               txtUserID.Text.ToString()+";"+txtUserPassword.Text.ToString();

            var param = mmpk_info+"  "+url_id_pass;
            var result = RunPy("UploadMMPK.py", param);

            return result;
        }

        private string GetRandomStringForFileName(int length)
        {
            var str = System.IO.Path.GetRandomFileName().Replace(".", "");
            return str.Substring(0, length);
        }

        private async void RemoveAllTempLayer(int vtpk_count)
        {
            await QueuedTask.Run(() =>
            {
                // 移除 clip 的兩圖層
                var clipGraphicLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<GraphicsLayer>()
                                        .Where(e => e.Name == clipPolygonName).FirstOrDefault();
                if (clipGraphicLayer != null)
                    MapView.Active.Map.RemoveLayer(clipGraphicLayer);
                var clipFeatureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                                        .Where(e => e.Name == clipPolygonName).FirstOrDefault();
                if (clipFeatureLayer != null)
                    MapView.Active.Map.RemoveLayer(clipFeatureLayer);
            });
        }

        private async void btnGenMmpkGo_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("確定要執行嗎?", "執行確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var response = "Failed,不明";

                // 之前有 clipPolygon Feature 圖層先移除
                await QueuedTask.Run(() =>
                {
                    var clipPolygonLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                                        .Where(e => e.Name == clipPolygonName).FirstOrDefault();
                    if (clipPolygonLayer != null)
                        MapView.Active.Map.RemoveLayer(clipPolygonLayer);
                });

                // 取下所有圖層(因之後會有自動添加的，會變化)
                List<Layer> layers = new List<Layer>();
                IEnumerable<Layer> lrs = MapView.Active.Map.Layers;
                foreach (Layer layer in lrs)
                    layers.Add(layer);

                // Step-1: 檢查是否有 MapServer，並取下此圖層 Extent
                Envelope genExtent = null;
                foreach (Layer layer in layers)
                {
                    var mtype = layer.GetType().ToString();
                    if( mtype.Contains("Tiled") )                // 以此判斷是否是影像
                    {
                        await QueuedTask.Run(() =>
                        {
                            genExtent = layer.QueryExtent();
                        });
                        break;      // 以首個為範圍
                    }
                }      

                if( genExtent == null ) 
                {
                    // 無影像圖層改用 map extent
                    genExtent = MapView.Active.Extent;    
                }

                // 由 extent 建切割 polygon 圖層
                Polygon polygon = null;                    
                await QueuedTask.Run(() =>
                {
                    polygon = PolygonBuilderEx.CreatePolygon(genExtent);                    
                });

                // 建立 polygon GraphicLayer 圖層準備供切割圖資用
                GraphicsLayer myGraphicLayer = null;
                var CreationParams = new GraphicsLayerCreationParams
                {
                    Name = clipPolygonName,
                    MapMemberPosition = MapMemberPosition.AutoArrange
                };
                await QueuedTask.Run(() =>
                {
                    myGraphicLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(
                                         CreationParams, MapView.Active.Map);
                });
                // 產生 polygon 圖徵
                CIMGraphic polygonGraphic = null;
                CIMStroke outline = SymbolFactory.Instance.ConstructStroke(
                        ColorFactory.Instance.BlueRGB, 2.0, SimpleLineStyle.DashDotDot);
                CIMPolygonSymbol polySym = SymbolFactory.Instance.ConstructPolygonSymbol(
                        ColorFactory.Instance.RedRGB, SimpleFillStyle.ForwardDiagonal, outline);
                await QueuedTask.Run(() =>
                {
                    polygonGraphic = GraphicFactory.Instance.CreateSimpleGraphic(polygon,polySym);
                    myGraphicLayer.AddElement(polygonGraphic);
                    myGraphicLayer.SetVisibility(true);
                });              

                // 切割圖層轉成 Feature(此因切割需用 Feature 為參數)
                await QueuedTask.Run(() =>
                { 
                    clipLayerToFeatures(clipPolygonName);
                });

                // 切割 Layer 設為不可見
                await QueuedTask.Run(() =>
                { 
                    var clipPolygonLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                                        .Where(e => e.Name == clipPolygonName).FirstOrDefault();            
                    if (clipPolygonLayer != null)
                        clipPolygonLayer.SetVisibility(false);
                });

                // Step-2: 開始對每一個圖層處理，影像則製成 tbkx，向量則製成 vtpk
                int tpkx_count = 0;
                int vtpk_count = 0;

                // 建立及製作所需環境
                CheckAndClearTemp();

                MessageBox.Show("開始製作，可能要花些時間請等待至完成，按鍵繼續","通知");
                foreach (Layer layer in layers)
                {
                    var mtype = layer.GetType().ToString();
                    bool bo = (bool)chkNoTpkx.IsChecked;
                    var url = layer?.GetType()?.GetProperty("URL")?.GetValue(layer).ToString();
                    // 開始處理此圖層
                    await QueuedTask.Run(async() =>
                    {
                        // 影像製作成 tbkx                        
                        if ( mtype.Contains("Tiled") && !bo )
                        {                            
                            // 以流水號命名
                            tpkx_count = tpkx_count + 1;
                            //var tpkx_name = txtMmpkName.Text.ToString();
                            //tpkx_name += "_tpkx_"+tpkx_count.ToString();
                            var tpkx_name = "tpkx_"+tpkx_count.ToString();

                            // 先以切割方式下載影像才可製作
                            var clip_file = tempRootPath+"clipRaster_"+tpkx_count.ToString()+".tif";
                            await QueuedTask.Run(() =>
                            {
                                getClipRaster(url, clip_file, genExtent);
                            });
                            response = CreateTPKX(clip_file,tpkx_name);
                            if( response.Substring(0,6).Equals("Failed") )
                            {
                                MessageBox.Show(response.Substring(8), "轉tpkx失敗");
                                RemoveAllTempLayer(vtpk_count);
                                return;
                            }
                        }
                        // feature layer 轉 vtpk
                        else if( mtype.Contains("Feature") )
                        {
                            // 以流水號命名
                            vtpk_count = vtpk_count + 1;
                            //var vtpk_name = txtMmpkName.Text.ToString();
                            //vtpk_name += "_vtpk_"+vtpk_count.ToString();
                            var vtpk_name = "vtpk_"+vtpk_count.ToString();

                            // 先以切割方式取下
                            var clip_name = "clipTemp_"+vtpk_count.ToString();
                            var clip_file = tempRootPath+clip_name+".shp";

                            // 產生此向量 Feature 的切割
                            await QueuedTask.Run(() =>
                            {
                                getClipFeature(layer.Name.ToString(), clip_file, clipPolygonName);
                            });

                            // Pro 會自動將此 Feature 加到 Map，此無必要，移除
                            await QueuedTask.Run(() =>
                            {
                                var clipLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                                        .Where(e => e.Name == clip_name).FirstOrDefault();            
                                if (clipLayer != null)
                                    MapView.Active.Map.RemoveLayer(clipLayer);
                            });

                            response = CreateVTPK(clip_file,vtpk_name);
                            if( response.Substring(0,6).Equals("Failed") )
                            {
                                MessageBox.Show(response.Substring(8), "轉vtpk失敗");
                                RemoveAllTempLayer(vtpk_count);
                                return;
                            }
                        }
                    });
                }

                // Step-3: 對所產生的 tpkx vtpk 開始打包成 mmpk
                var mmpk_path = tempRootPath+GetRandomStringForFileName(10)+".mmpk";    
                response = CreateMMPK(mmpk_path,vtpk_count,tpkx_count);
                if( response.Substring(0,6).Equals("Failed") )
                {
                    MessageBox.Show(response.Substring(8),"轉 mmpk 失敗");
                    return;
                }

                // Step-4: 將 mmpk 上傳
                response = UploadMMPK(mmpk_path);
                if( response.Substring(0,6).Equals("Failed") )
                {
                    MessageBox.Show(response.Substring(8),"上傳 mmpk 到 Portal 失敗");
                    RemoveAllTempLayer(vtpk_count);
                    return;
                }

                // 完成
                MessageBox.Show("完成!!，可至 Portal 上檢查及下載該 mmpk","通知");
                RemoveAllTempLayer(vtpk_count);
            }
        }

        private void btnSaveParam_Click(object sender, RoutedEventArgs e)
        {
            SaveMmpkParam();
            MessageBox.Show("已存入!!，按鍵繼續","通知");
        }

        private void txtTempPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            tempRootPath = txtTempPath.Text.ToString();
        }
    }

}
