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
        { get; set; }
        public string mPackageName
        { get; set; }
        public string mSavePath
        { get; set; }
        private bool mBinary;
        private int mSTLUnits;
        private int mSTLQuality;
        private bool mshowInfo;
        private bool mSTLPreview;
        public List<link> mLinks
        { get; set; }
        ModelDoc2 ActiveSWModel;
        #endregion

        public SW2URDFExporter(ISldWorks iSldWorksApp)
        {
            iSwApp = iSldWorksApp;
            ActiveSWModel = default(ModelDoc2);
            ActiveSWModel = (ModelDoc2)iSwApp.ActiveDoc;
            mSavePath = System.Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            mPackageName = ActiveSWModel.GetTitle();
        }

        public void createRobotFromActiveModel()
        {
            mRobot = new robot();
            mRobot.name = ActiveSWModel.GetTitle();

            int modelType = ActiveSWModel.GetType();
            if (modelType == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                mRobot.BaseLink = getBaseLinkFromActiveModel();
            }
            else if (modelType == (int)swDocumentTypes_e.swDocPART)
            {
                mRobot.BaseLink = getLinkFromActiveModel();
            }


        }

        public link getBaseLinkFromActiveModel()
        {
            return getBaseLinkFromAssy(ActiveSWModel);
        }

        public link getBaseLinkFromAssy(ModelDoc2 swModel)
        {
            AssemblyDoc swAssy = (AssemblyDoc)swModel;
            object[] varComp = swAssy.GetComponents(true);

            link baseLink = new link();
            //link baseLink = assignParentLinks(sparseLink, 0);
            foreach (IComponent2 comp in varComp)
            {
                link sparseLink = createSparseBranchFromComponents(comp, 0);
                baseLink.Children.Add(sparseLink);
            }

            return assignParentLinks(baseLink, 0);
        }

        public link getLinkFromComp(object comp, int level)
        {
            IComponent2 parentComp = (IComponent2)comp;
            ModelDoc2 parentdoc = parentComp.GetModelDoc();
            link Link;

            if (parentdoc == null)
            {
                //parentdoc can sometimes be null here!
                throw new System.InvalidOperationException("Component " + parentComp.Name2 + " is null");
            }
            if (parentdoc.GetType() == (int)swDocumentTypes_e.swDocPART)
            {
                Link = getLinkFromPartModel(parentdoc);
                Link.SWComponent = parentComp;
                Link.SWComponentLevel = level;
                return Link;
            }
            else
            {
                object[] children = parentComp.GetChildren();
                link parent = assignParentLinkFromChildren(children, parentComp.GetModelDoc());
                //children.Remove(parent);
                parent.SWComponentLevel += level;

                foreach (IComponent2 child in children)
                {
                    if (!child.IsHidden(true))
                    {
                        parent.Children.Add(getLinkFromComp(child, level + 1));
                    }

                }
                //parent.SWComponent = parentComp;
                return parent;
            }

        }

        public link createSparseBranchFromComponents(IComponent2 comp, int level)
        {
            link Link = new link();
            if (comp.IGetChildrenCount() == 0)
            {
                Link = getLinkFromComp(comp, level);
            }
            else
            {
                object[] children = comp.GetChildren();
                foreach (IComponent2 child in children)
                {
                    Link.Children.Add(createSparseBranchFromComponents(child, level + 1));
                }
            }
            Link.SWComponent = comp;
            Link.SWComponentLevel = level;
            return Link;
        }

        public link assignParentLinks(link top, int level)
        {
            top = findParent(top, level + 1);
            foreach (link child in top.Children)
            {
                if (!child.SWComponent.IsHidden(true))
                {
                    top.Children.Add(assignParentLinks(child, level + 1));
                    top.Children.Remove(child);
                }
            }
            return top;
        }

        public link findParent(link top, int level)
        {
            if (top.Children.Count > 0)
            {
                link AssignedParentLink = new link();
                ModelDoc2 ParentDoc;

                int priorityLevel = -1;
                // Iteratively going through SolidWorks component structure to find the 'best' choice for the parent link

                double largestFixedVolume = 0;
                double largestPartVolume = 0;
                double largestAssyVolume = 0;

                ParentDoc = top.SWComponent.GetModelDoc();
                int ParentType = ParentDoc.GetType();
                foreach (link child in top.Children)
                {
                    if (!child.SWComponent.IsHidden(true))
                    {
                        ModelDoc2 ChildDoc = child.SWComponent.GetModelDoc();
                        if (ChildDoc == null)
                        {
                            throw new System.InvalidOperationException("Component " + child.SWComponent.Name2 + " is null");
                        }
                        int ChildType = (int)ChildDoc.GetType();

                        IMassProperty childMass = ChildDoc.Extension.CreateMassProperty();

                        double childVolume = childMass.Volume;

                        //Highest priority is the largest fixed component
                        if (child.SWComponent.IsFixed() && childMass.Volume > largestFixedVolume)
                        {
                            priorityLevel = 2;
                            AssignedParentLink = child;
                            largestFixedVolume = childVolume;
                        }
                        //Second highest priority is the largest floating part
                        else if (childMass.Volume > largestPartVolume && ChildType == (int)swDocumentTypes_e.swDocPART && priorityLevel < 2)
                        {
                            priorityLevel = 1;
                            AssignedParentLink = child;
                            largestPartVolume = childVolume;
                        }
                        //Third priority is the 'best' choice from the largest assembly
                        else if (childMass.Volume > largestAssyVolume && ChildType == (int)swDocumentTypes_e.swDocASSEMBLY && priorityLevel < 1)
                        {
                            priorityLevel = 0;
                            AssignedParentLink = child;
                            largestAssyVolume = childVolume;
                        }
                    }
                }
                if (priorityLevel == 2 && ParentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    return findParent(AssignedParentLink, level + 1);

                }
                // If no parts were found, then the largest assembly will be iterated through to find the best choice
                else if (priorityLevel == 0)
                {
                    return findParent(AssignedParentLink, level + 1);
                }
                top.Children.Remove(AssignedParentLink);
                AssignedParentLink.Children.AddRange(top.Children);
                return AssignedParentLink;
            }
            return top;
        }

        //This method could stand to be cleaned up
        public link assignParentLinkFromChildren(object[] children, ModelDoc2 ParentDoc)
        {
            int descentLevel = 0;
            link Link = new link();
            IComponent2 ParentComp = default(IComponent2);

            bool foundParent = false;
            foreach (IComponent2 child in children)
            {
                if (!child.IsHidden(true))
                {
                    ParentComp = child;
                    foundParent = true;
                    break;
                }
            }
            if (!foundParent)
            {
                throw new System.InvalidOperationException("All components are either hidden or suppressed");
            }
            ModelDoc2 modeldoc = ParentDoc;

            int priorityLevel = -1;
            // Iteratively going through SolidWorks component structure to find the 'best' choice for the parent link
            while (priorityLevel < 0)
            {
                double largestFixedVolume = 0;
                double largestPartVolume = 0;
                double largestAssyVolume = 0;

                ParentDoc = ParentComp.GetModelDoc();
                int ParentType = ParentDoc.GetType();
                foreach (IComponent2 child in children)
                {
                    if (!child.IsHidden(true))
                    {
                        ModelDoc2 ChildDoc = child.GetModelDoc();
                        if (ChildDoc == null)
                        {
                            throw new System.InvalidOperationException("Component " + child.Name2 + " is null");
                        }
                        int ChildType = (int)ChildDoc.GetType();

                        IMassProperty childMass = ChildDoc.Extension.CreateMassProperty();

                        double childVolume = childMass.Volume;

                        //Highest priority is the largest fixed component
                        if (child.IsFixed() && childMass.Volume > largestFixedVolume)
                        {
                            priorityLevel = 2;
                            ParentComp = child;
                            largestFixedVolume = childVolume;
                        }
                        //Second highest priority is the largest floating part
                        else if (childMass.Volume > largestPartVolume && ChildType == (int)swDocumentTypes_e.swDocPART && priorityLevel < 2)
                        {
                            priorityLevel = 1;
                            ParentComp = child;
                            largestPartVolume = childVolume;
                        }
                        //Third priority is the 'best' choice from the largest assembly
                        else if (childMass.Volume > largestAssyVolume && ChildType == (int)swDocumentTypes_e.swDocASSEMBLY && priorityLevel < 1)
                        {
                            priorityLevel = 0;
                            ParentComp = child;
                            largestAssyVolume = childVolume;
                        }
                    }
                }

                ParentDoc = ParentComp.GetModelDoc();
                ParentType = ParentDoc.GetType();
                // If a fixed component was found that is an assembly, its children will be iterated through on the next run
                if (priorityLevel == 2 && ParentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    priorityLevel = -1;
                    children = ParentComp.GetChildren();
                    descentLevel++;
                }
                // If no parts were found, then the largest assembly will be iterated through to find the best choice
                else if (priorityLevel == 0)
                {
                    priorityLevel = -1;
                    children = ParentComp.GetChildren();
                    descentLevel++;
                }
                // Otherwise, if a part was finally selected for parent status, the parentdoc is selected and it is converted into a link
            }
            if (ParentDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                throw new System.InvalidOperationException("Parent link cannot be made from assembly");
            }
            Link = getLinkFromPartModel(ParentDoc);
            Link.SWComponent = ParentComp;
            Link.SWComponentLevel = descentLevel;
            return Link;
        }


        public void createJoints()
        {
            mRobot.BaseLink = createChildLinks(mRobot.BaseLink);
        }

        public link createChildLinks(link parent)
        {
            foreach (link child in parent.Children)
            {
                child.Joint = createJointFromLinks(parent, child);
                createChildLinks(child);
            }
            return parent;
        }

        public joint createJointFromLinks(link parent, link child)
        {
            joint Joint = new joint();
            Joint.name = parent.name + "_to_" + child.name;

            Joint.Origin.X = parent.Inertial.Origin.X - child.Inertial.Origin.X;
            Joint.Origin.Y = parent.Inertial.Origin.Y - child.Inertial.Origin.Y;
            Joint.Origin.Z = parent.Inertial.Origin.Z - child.Inertial.Origin.Z;
            Joint.Origin.Roll = parent.Inertial.Origin.Roll - child.Inertial.Origin.Roll;
            Joint.Origin.Pitch = parent.Inertial.Origin.Pitch - child.Inertial.Origin.Pitch;
            Joint.Origin.Yaw = parent.Inertial.Origin.Yaw - child.Inertial.Origin.Yaw;

            Joint.type = "fixed";
            Joint.Axis.XYZ = new double[] { 1, 0, 0 };

            Joint.Parent.name = parent.name;
            Joint.Child.name = child.name;

            Joint.Dynamics.friction = 0;
            Joint.Dynamics.damping = 0;

            Joint.Calibration.rising = 0;
            Joint.Calibration.falling = 0;

            Joint.Limit.upper = 0;
            Joint.Limit.lower = 0;
            Joint.Limit.effort = 0;
            Joint.Limit.velocity = 0;

            Joint.Safety.soft_upper = 0;
            Joint.Safety.soft_lower = 0;
            Joint.Safety.k_velocity = 0;
            Joint.Safety.k_position = 0;

            return Joint;
        }

        public void exportRobot()
        {
            //Creating package directories
            URDFPackage package = new URDFPackage(mPackageName, mSavePath);
            package.createDirectories();
            string windowsURDFFileName = package.WindowsRobotsDirectory + mRobot.BaseLink.name + ".URDF";

            //Customizing STL preferences to how I want them
            saveUserPreferences();
            setSTLExportPreferences();

            //Saving part as STL mesh
            string filename = exportMeshes(mRobot.BaseLink, package);
            mRobot.BaseLink.Visual.Geometry.Mesh.filename = filename;
            mRobot.BaseLink.Collision.Geometry.Mesh.filename = filename;

            //Writing URDF to file
            URDFWriter uWriter = new URDFWriter(windowsURDFFileName);
            mRobot.writeURDF(uWriter.writer);

            resetUserPreferences();
        }

        public string exportMeshes(link Link, URDFPackage package)
        {
            foreach (link child in Link.Children)
            {
                string filename = exportMeshes(child, package);
                child.Visual.Geometry.Mesh.filename = filename;
                child.Collision.Geometry.Mesh.filename = filename;
            }
            string meshFileName = package.MeshesDirectory + Link.name + ".STL";
            string windowsMeshFileName = package.WindowsMeshesDirectory + Link.name + ".STL";

            int errors = 0;
            int warnings = 0;

            ModelDoc2 modeldoc = Link.SWComponent.GetModelDoc2();

            iSwApp.ActivateDoc3(Link.name + ".sldprt", false, (int)swRebuildOnActivation_e.swUserDecision, ref errors);
            modeldoc = iSwApp.IActiveDoc2;
            modeldoc.Extension.SaveAs(windowsMeshFileName, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
            iSwApp.CloseDoc(Link.name + ".sldprt");
            return meshFileName;
        }
        #region Part Exporting methods
        public link getLinkFromPartModel(ModelDoc2 swModel)
        {
            link Link = new link();
            Link.name = swModel.GetTitle();

            //Get link properties from SolidWorks part
            IMassProperty swMass = swModel.Extension.CreateMassProperty();
            Link.Inertial.Mass.Value = swMass.Mass;

            Link.Inertial.Inertia.Moment = swMass.GetMomentOfInertia((int)swMassPropertyMoment_e.swMassPropertyMomentAboutCenterOfMass); // returned as double with values [Lxx, Lxy, Lxz, Lyx, Lyy, Lyz, Lzx, Lzy, Lzz]

            double[] centerOfMass = swMass.CenterOfMass;
            Link.Inertial.Origin.XYZ = centerOfMass;
            Link.Inertial.Origin.RPY = new double[3] { 0, 0, 0 };
            Link.Visual.Origin.XYZ = centerOfMass;
            Link.Visual.Origin.RPY = new double[3] { 0, 0, 0 };
            Link.Collision.Origin.XYZ = centerOfMass;
            Link.Collision.Origin.RPY = new double[3] { 0, 0, 0 };

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
            string meshFileName = package.MeshesDirectory + mRobot.BaseLink.name + ".STL";
            string windowsMeshFileName = package.WindowsMeshesDirectory + mRobot.BaseLink.name + ".STL";
            string windowsURDFFileName = package.WindowsRobotsDirectory + mRobot.BaseLink.name + ".URDF";

            //Customizing STL preferences to how I want them
            saveUserPreferences();
            setSTLExportPreferences();
            int errors = 0;
            int warnings = 0;

            //Saving part as STL mesh
            ActiveSWModel.Extension.SaveAs(windowsMeshFileName, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
            mRobot.BaseLink.Visual.Geometry.Mesh.filename = meshFileName;
            mRobot.BaseLink.Collision.Geometry.Mesh.filename = meshFileName;

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



//ModelDoc2 ChildDoc = child.GetModelDoc();
//int ChildType = (int)ChildDoc.GetType();
//IMassProperty childMass = ChildDoc.Extension.CreateMassProperty();

//double childVolume = childMass.Volume;

////Highest priority is the largest fixed component
//if (child.IsFixed() && childMass.Volume > largestFixedVolume)
//{
//    priorityLevel = 2;
//    AssignedParentComp = child;
//    largestFixedVolume = childVolume;
//}
////Second highest priority is the largest floating part
//else if (childMass.Volume > largestPartVolume && ChildType == (int)swDocumentTypes_e.swDocPART && priorityLevel < 2)
//{
//    priorityLevel = 1;
//    AssignedParentComp = child;
//    largestPartVolume = childVolume;
//}
////Third priority is the 'best' choice from the largest assembly
//else if (childMass.Volume > largestAssyVolume && ChildType == (int)swDocumentTypes_e.swDocASSEMBLY && priorityLevel < 1)
//{
//    priorityLevel = 0;
//    AssignedParentComp = child;
//    largestAssyVolume = childVolume;
//} 