using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Threading.Tasks;

namespace FGISMmpkAddin
{
  internal class AutoMap2Mmpk : Module
    {
        private static AutoMap2Mmpk _this = null;

        public static AutoMap2Mmpk Current
        {
            get
            {
                return _this ?? (_this = (AutoMap2Mmpk)FrameworkApplication.FindModule(
                   "FGISMmpkAddin_Module"));
            }
        }

        #region Overrides
        protected override bool CanUnload()
        {
            //TODO - add your business logic
            //return false to ~cancel~ Application close
            return true;
        }

        #endregion Overrides

    }
}
