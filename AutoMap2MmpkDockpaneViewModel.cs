using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace FGISMmpkAddin
{
    internal class AutoMap2MmpkButton : Button
    {
        protected override void OnClick()
        {
            AutoMap2MmpkDockpaneViewModel.Show();
        }
    }

    internal class AutoMap2MmpkDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "FGISMmpkAddin_AutoMap2MmpkDockpane";

        protected AutoMap2MmpkDockpaneViewModel()
        {
        }

        protected override Task InitializeAsync() {
            return base.InitializeAsync();
        }        
        
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;
            pane.Activate();
        }

    }

}
