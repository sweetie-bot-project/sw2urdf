﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorks.Interop.swconst;
using SolidWorksTools;
using SolidWorksTools.File;
namespace SW2URDF
{
    public partial class PMLinkTree : Form
    {
        public URDFExporter Exporter;
        public URDFExporterPM propMgr;
        public PMLinkTree(URDFExporterPM sPropMgr, URDFExporter sExporter)
        {
            propMgr = sPropMgr;
            Exporter = sExporter;
            InitializeComponent();

        }

        private void treeView_linkTree_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }

 
        
    }
}
