﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Reflection;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorks.Interop.swconst;
using SolidWorksTools;
using SolidWorksTools.File;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace SW2URDF
{
    public class SW2URDFExporter
    {
         #region Local variables
        ISldWorks iSwApp = null;
        ICommandManager iCmdMgr = null;

        public robot mRobot
        {get; set;}
        public link mLink
        { get; set; }
        public string mPackageName
        { get; set; }
        public string mSavePath
        { get; set;}
        private bool mBinary;
        private int mSTLUnits;
        private int mSTLQuality;
        private bool mshowInfo;
        private bool mSTLPreview;
        public List<link> mLinks
        { get; set; }
        ModelDoc2 ActiveSWModel;
        object[] varComp;
        #endregion

        public SW2URDFExporter(ISldWorks iSldWorksApp)
        {
            iSwApp = iSldWorksApp;
            ActiveSWModel = default(ModelDoc2);
            ActiveSWModel = (ModelDoc2)iSwApp.ActiveDoc;
            mSavePath = System.Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            mPackageName = ActiveSWModel.GetTitle();
            //mRobot = getRobotFromActiveModel();    
        }

        public robot getRobotFromActiveModel()
        {
            robot Robot = new robot();

            int modelType = ActiveSWModel.GetType();
            if (modelType == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                mRobot.setBaseLink(getBaseLinkFromActiveModel());
            }
            else if (modelType == (int)swDocumentTypes_e.swDocPART)
            {
                Robot.setBaseLink(getLinkFromActiveModel());
            }

            return Robot;
        }

        public link getBaseLinkFromAssy(ModelDoc2 swModel)
        {
            AssemblyDoc swAssy = (AssemblyDoc)swModel;
            varComp = (object[])swAssy.GetComponents(true);

            link baseLink = assignParentLinkFromChildren(varComp, swModel);
            
            foreach (IComponent comp in varComp)
            {
                baseLink.Children.Add(getLinkFromComp(comp));
            }

            return baseLink;
        }

        public link getBaseLinkFromActiveModel()
        {
            return getBaseLinkFromAssy(ActiveSWModel);
        }

        public link getLinkFromComp(object comp)
        {
            IComponent parentComp = (IComponent)comp;
            object[] children = parentComp.GetChildren();

            link parent = assignParentLinkFromChildren(children, parentComp.GetModelDoc());

            foreach (object child in children)
            {
                parent.Children.Add(getLinkFromComp(child));
            }

            return parent;
        }

        public link assignParentLinkFromChildren(object[] children, ModelDoc2 ParentDoc)
        {
            IComponent ParentComp = (IComponent)children[0];
            ModelDoc2 modeldoc = ParentDoc;

            if (modeldoc.GetType() == (int)swDocumentTypes_e.swDocPART)
            {
                return getLinkFromPartModel(modeldoc);
            }
            else
            {
                int priorityLevel = -1;
                // Iteratively going through SolidWorks component structure to find the 'best' choice for the parent link
                while (priorityLevel < 0)
                {
                    double largestFixedVolume = 0;
                    double largestPartVolume = 0;
                    double largestAssyVolume = 0;
                    foreach (IComponent child in children)
                    {
                        IMassProperty childMass = child.GetModelDoc().childdoc.Extension.CreateMassProperty();
                        double[] bb = child.GetBox(false, true);
                        double childBBVolume = boundingBoxVolume(bb);

                        //Highest priority is the largest fixed component
                        if (child.IsFixed() && childMass.Volume > largestFixedVolume)
                        {
                            priorityLevel = 2;
                            ParentComp = child;
                            largestFixedVolume = childBBVolume;
                        }
                        //Second highest priority is the largest floating part
                        else if (childMass.Volume > largestPartVolume && child.GetModelDoc().GetType() == (int)swDocumentTypes_e.swDocPART && priorityLevel < 2)
                        {
                            priorityLevel = 1;
                            ParentComp = child;
                            largestPartVolume = childBBVolume;
                        }
                        //Third priority is the 'best' choice from the largest assembly
                        else if (childMass.Volume > largestAssyVolume && child.GetModelDoc().GetType() == (int)swDocumentTypes_e.swDocASSEMBLY && priorityLevel < 1)
                        {
                            priorityLevel = 0;
                            ParentComp = child;
                            largestAssyVolume = childBBVolume;
                        }
                    }
                    // If a fixed component was found that is an assembly, its children will be iterated through on the next run
                    if (priorityLevel == 2 && ParentDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        priorityLevel = -1;
                        children = ParentComp.GetChildren();
                    }
                    // If no parts were found, then the largest assembly will be iterated through to find the best choice
                    else if (priorityLevel == 0)
                    {
                        priorityLevel = -1;
                        children = ParentComp.GetChildren();
                    }
                    // Otherwise, if a part was finally selected for parent status, the parentdoc is selected and it is converted into a link
                    else
                    {
                        ParentDoc = ParentComp.GetModelDoc();
                    }
                }
                return getLinkFromPartModel(ParentDoc);
            }
        }

        public double boundingBoxVolume(double[] bb)
        {
            return ((bb[3] - bb[0]) * (bb[4] - bb[1]) * (bb[5] - bb[2]));
        }

        
        #region Part Exporting methods
        public link getLinkFromPartModel(ModelDoc2 swModel)
        {
            link Link = new link();
            Link.name = swModel.FeatureManager.FeatureStatistics.PartName;
            
            //Get link properties from SolidWorks part
            IMassProperty swMass = swModel.Extension.CreateMassProperty();
            Link.Inertial.Mass.Value = swMass.Mass;
            
            Link.Inertial.Inertia.Moment = swMass.GetMomentOfInertia((int)swMassPropertyMoment_e.swMassPropertyMomentAboutCenterOfMass); // returned as double with values [Lxx, Lxy, Lxz, Lyx, Lyy, Lyz, Lzx, Lzy, Lzz]
            
            double[] centerOfMass = swMass.CenterOfMass;
            Link.Inertial.Origin.XYZ = centerOfMass;
            Link.Inertial.Origin.RPY = new double[3] {0, 0, 0};
            Link.Visual.Origin.XYZ = centerOfMass;
            Link.Visual.Origin.RPY = new double[3] {0, 0, 0};
            Link.Collision.Origin.XYZ = centerOfMass;
            Link.Collision.Origin.RPY = new double[3] {0, 0, 0};

            return Link;
        }

        public link getLinkFromActiveModel()
        {
            return getLinkFromPartModel(ActiveSWModel);
        }


        public void exportLink()
        {
            //Creating package directories
            URDFPackage package = new URDFPackage(mPackageName, mSavePath);
            package.createDirectories();
            string meshFileName = package.MeshesDirectory + mLink.name + ".STL";
            string windowsMeshFileName = package.WindowsMeshesDirectory + mLink.name + ".STL";
            string windowsURDFFileName = package.WindowsRobotsDirectory + mLink.name + ".URDF";

            //Customizing STL preferences to how I want them
            saveUserPreferences();
            setSTLExportPreferences();
            int errors = 0;
            int warnings = 0;

            //Saving part as STL mesh
            ActiveSWModel.Extension.SaveAs(windowsMeshFileName, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
            mLink.Visual.Geometry.Mesh.filename = meshFileName;
            mLink.Collision.Geometry.Mesh.filename = meshFileName;

            //Writing URDF to file
            URDFWriter uWriter = new URDFWriter(windowsURDFFileName);
            //mRobot.addLink(mLink);
            mRobot.writeURDF(uWriter.writer);

            resetUserPreferences();
        }
        #endregion


        #region STL Preference shuffling
        public void saveUserPreferences()
        {
            mBinary = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat);
            mSTLUnits = iSwApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits);
            mSTLQuality = iSwApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality);
            mshowInfo = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave);
            mSTLPreview = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview);
        }

        public void setSTLExportPreferences()
        {
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, 2);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Coarse);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, false);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, false);
        }

        public void resetUserPreferences()
        {
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, mBinary);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, mSTLUnits);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, mSTLQuality);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, mshowInfo);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, mSTLPreview);
        }
        #endregion
    }
}
