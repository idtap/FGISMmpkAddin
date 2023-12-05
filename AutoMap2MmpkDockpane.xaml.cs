
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
    }

    public partial class AutoMap2MmpkDockpaneView : UserControl
    {
        private static string mmpkParamPath = Utility.AddinAssemblyLocation()+"/Images/mmpkParam.json";
        private static string pathProExe = null;
        private static string pathPython = null;

        public AutoMap2MmpkDockpaneView()
        {
            InitializeComponent();
            initNeed();
            LoadMmpkParam();
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
                UserPassword =txtUserPassword.Text.ToString()
            };
            tempLists.Add(param);
            Utility.SaveToJsonFile(mmpkParamPath, tempLists);
        }
        
        private void CheckAndClearTemp()
        {
            // 預計輸出至 C:\Temp，檢查及清除
            if( Directory.Exists("C:\\Temp") == false )
                Directory.CreateDirectory("C:\\Temp");
            // 複製空白 .aprx 檔
            var empty_aprx = System.IO.Path.Combine(pathPython, "empty.aprx");
            if (System.IO.File.Exists("C:\\Temp\\Temp.aprx"))
                System.IO.File.Delete("C:\\Temp\\Temp.aprx");
            System.IO.File.Copy(empty_aprx,"C:\\Temp\\Temp.aprx");
        }

        private string CreateTPKX( string sour_url, string tpkx_name )
        {
            var tpkx_fullname = "C:\\Temp\\"+tpkx_name+".tpkx";
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

        private async void btnGenMmpkGo_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("確定要執行嗎?", "執行確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // Step-1: 檢查是否有 MapServer，並取下此圖層 Extent
                Envelope genExtent = null;
                IEnumerable<Layer> layers = MapView.Active.Map.Layers;
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
                    MessageBox.Show("查無掛入任何影像圖層無法繼續，請檢查後再試","錯誤");
                    return;
                }

                // 由 extent 建切割 polygon 圖層
                Polygon polygon = PolygonBuilderEx.CreatePolygon(genExtent);
                CIMGraphic polygonGraphic = null;
                //await QueuedTask.Run(() =>
                //{
                //    polygonGraphic = GraphicFactory.Instance.CreateSimpleGraphic(polygon);
                //});
                var CreationParams = new GraphicsLayerCreationParams
                {
                    Name = "Clip Polygon",
                    MapMemberPosition = MapMemberPosition.AutoArrange
                };
                //var myGraphicLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(
                //            CreationParams, MapView.Active.Map);
                var _graphic = Utility.Graphics("Clip Polygon");

                CIMStroke outline = SymbolFactory.Instance.ConstructStroke(
                    ColorFactory.Instance.BlueRGB, 2.0, SimpleLineStyle.DashDotDot);
                CIMPolygonSymbol polySym = SymbolFactory.Instance.ConstructPolygonSymbol(
                    ColorFactory.Instance.RedRGB, SimpleFillStyle.ForwardDiagonal, outline);
                await QueuedTask.Run(() =>
                {
                    _graphic.Add(MapView.Active.AddOverlay(polygon, polySym.MakeSymbolReference()));
                    //myGraphicLayer.AddElement(polygonGraphic);
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
                    // 影像製作成 tbkx
                    bool bo = (bool)chkNoTpkx.IsChecked;
                    if ( mtype.Contains("Tiled") && !bo )
                    {
                        var url = layer?.GetType()?.GetProperty("URL")?.GetValue(layer).ToString();

                        // 以流水號命名
                        tpkx_count = tpkx_count + 1;
                        var tpkx_name = txtMmpkName.Text.ToString();
                        tpkx_name += "_tpkx_"+tpkx_count.ToString();

                        // 先以切割方式下載影像才可製作
                        var clip_file = "C:\\Temp\\clipRaster_"+tpkx_count.ToString()+".tif";
                        await QueuedTask.Run(() =>
                        {
                            getClipRaster(url, clip_file, genExtent);
                        });
                        var result2 = CreateTPKX(clip_file,tpkx_name);
                        if( result2.Substring(0,6).Equals("Failed") )
                        {
                            MessageBox.Show(layer.URI.ToString(),"轉tpkx失敗");
                            return;
                        }
                    }
                    // feature layer 轉 vtpk
                    else if( mtype.Contains("Feature") )   
                    {
                        var url = layer?.GetType()?.GetProperty("URL")?.GetValue(layer).ToString();

                        // 以流水號命名
                        vtpk_count = vtpk_count + 1;
                        var vtpk_name = txtMmpkName.Text.ToString();
                        vtpk_name += "_vtpk_"+vtpk_count.ToString();

                        // 先以切割方式取下
                        var clip_file = "C:\\Temp\\clipTemp_"+tpkx_count.ToString()+".tif";
                        
                    }            
                }

                // Step-3: 對所產生的 tpkx vtpk 開始打包成 mmpk

                // Step-4: 將 mmpk 上傳

                // 完成
                MessageBox.Show("完成!!，可至 Portal 上檢查及下載該 mmpk","通知");
            }
        }

        private void btnSaveParam_Click(object sender, RoutedEventArgs e)
        {
            SaveMmpkParam();
            MessageBox.Show("已存入!!，按鍵繼續","通知");
        }
    }

}
