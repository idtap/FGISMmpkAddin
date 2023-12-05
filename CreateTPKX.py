# -*- coding: utf-8 -*-

import sys, getopt
import os
import math
import arcpy
from arcpy import *
from datetime import datetime, timedelta
import shutil

# 初始環境設定
arcpy.env.overwriteOutput = True
arcpy.ClearWorkspaceCache_management()

try:
    # 取下參數
    sour_tif = sys.argv[1]
    tpkx_name = sys.argv[2]

    arcpy.management.MakeRasterLayer(sour_tif,"Temp")
    arcpy.SaveToLayerFile_management("Temp", "C:\\Temp\\temp.lyrx")

    aprx = arcpy.mp.ArcGISProject("C:\\Temp\\Temp.aprx")
    df =  aprx.listMaps("Map")[0]      
    lf = arcpy.mp.LayerFile("C:\\Temp\\temp.lyrx")
    df.addLayer(lf)
    #aprx.save()
    arcpy.management.CreateMapTilePackage(
        df, 
        "ONLINE", 
        "C:\\Temp\\"+tpkx_name+".tpkx",
        "PNG8", 
        16
        )
    print("C:\\Temp\\"+tpkx_name+".tpkx")
    
except Exception as ex:
    print("Failed,"+repr(ex))

