using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Catalog;

namespace FGISMmpkAddin
{
    internal class AutoMap2MmpkButton : Button
    {
        private GenMap2MmpkWindow genMap2MmpkWindow = null;

        protected override void OnClick()
        {
            if (genMap2MmpkWindow != null)
            {
                genMap2MmpkWindow.Show();
                return;
            }
            GenMap2MmpkWindow_Create();
            genMap2MmpkWindow.Show();
        }

        public void GenMap2MmpkWindow_Create()
        {
            genMap2MmpkWindow = new GenMap2MmpkWindow();
            genMap2MmpkWindow.Owner = FrameworkApplication.Current.MainWindow;
            genMap2MmpkWindow.Closing += GenMap2MmpkWindow_Closing;
            genMap2MmpkWindow.Closed += (o, e) => { 
                //genMap2MmpkWindow = null; 
            };
        }

        void GenMap2MmpkWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            genMap2MmpkWindow.Hide();
        }

    }
}
