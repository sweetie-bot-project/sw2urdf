﻿using System;
using System.IO;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Collections.Generic;
using System.Xml.Serialization;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;


namespace SW2URDF
{
    // This class contains a long list of methods that are used throughout the export process. Methods for building links and joints are contained in here.
    // Many of the methods are overloaded, but seek to reduce repeated code as much as possible (i.e. the overloaded methods call eachother).
    // These methods are used by the PartExportForm, the AssemblyExportForm and the PropertyManager Page
    public partial class URDFExporter
    {
        #region class variables
        [XmlIgnore]
        public ISldWorks iSwApp = null;
        [XmlIgnore]
        private bool mBinary;
        private bool mshowInfo;
        private bool mSTLPreview;
        private bool mTranslateToPositive;
        private bool mSaveComponentsIntoOneFile;
        private int mSTLUnits;
        private int mSTLQuality;
        private double mHideTransitionSpeed;

        private UserProgressBar progressBar;

        [XmlIgnore]
        public ModelDoc2 ActiveSWModel;
        [XmlIgnore]
        public MathUtility swMath;
        [XmlIgnore]
        public Object swMathPID
        { get; set; }

        public robot mRobot
        { get; set; }
        public string mPackageName
        { get; set; }
        public string mSavePath
        { get; set; }
        public List<link> mLinks
        { get; set; }

        #endregion

        // Constructor for SW2URDF Exporter class
        public URDFExporter(ISldWorks iSldWorksApp)
        {
            constructExporter(iSldWorksApp);
            iSwApp.GetUserProgressBar(out progressBar);
            mSavePath = System.Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            mPackageName = ActiveSWModel.GetTitle();
            
        }

        private void constructExporter(ISldWorks iSldWorksApp)
        {
            iSwApp = iSldWorksApp;
            ActiveSWModel = (ModelDoc2)iSwApp.ActiveDoc;
            swMath = iSwApp.GetMathUtility();
        }

        #region Export Methods

        // Beginning method for exporting the full package
        public void exportRobot()
        {
            //Setting up the progress bar

            int progressBarBound = Common.getCount(mRobot.BaseLink);
            progressBar.Start(0, progressBarBound, "Creating package directories");

            //Creating package directories
            URDFPackage package = new URDFPackage(mPackageName, mSavePath);
            package.createDirectories();
            mRobot.name = mPackageName;
            string windowsURDFFileName = package.WindowsRobotsDirectory + mRobot.name + ".URDF";
            string windowsManifestFileName = package.WindowsPackageDirectory + "manifest.xml";

            //Creating manifest file
            manifestWriter manifestWriter = new manifestWriter(windowsManifestFileName);
            manifest Manifest = new manifest(mPackageName);
            Manifest.writeElement(manifestWriter);

            //Creating RVIZ launch file
            Rviz rviz = new Rviz(mPackageName, mSavePath);
            rviz.writeFiles(package.WindowsLaunchDirectory);

            //Creating Gazebo launch file
            Gazebo gazebo = new Gazebo(this.mRobot.name, this.mPackageName, this.mSavePath);
            gazebo.writeFile(package.WindowsLaunchDirectory);


            //Customizing STL preferences to how I want them
            saveUserPreferences();
            setSTLExportPreferences();

            //Saving part as STL mesh
            AssemblyDoc assyDoc = (AssemblyDoc)ActiveSWModel;
            List<Component2> hiddenComponents = Common.findHiddenComponents(assyDoc.GetComponents(false));
            ActiveSWModel.Extension.SelectAll();
            ActiveSWModel.HideComponent2();
            string filename = exportFiles(mRobot.BaseLink, package, 0);
            mRobot.BaseLink.Visual.Geometry.Mesh.filename = filename;
            mRobot.BaseLink.Collision.Geometry.Mesh.filename = filename;
            
            Common.showAllComponents(ActiveSWModel, hiddenComponents);
            //Writing URDF to file

            URDFWriter uWriter = new URDFWriter(windowsURDFFileName);
            mRobot.writeURDF(uWriter.writer);

            resetUserPreferences();
            progressBar.End();
        }

        //Recursive method for exporting each link (and writing it to the URDF)
        public string exportFiles(link Link, URDFPackage package, int count)
        {
            progressBar.UpdateProgress(count);
            progressBar.UpdateTitle("Exporting mesh: " + Link.name);
            // Iterate through each child and export its files
            foreach (link child in Link.Children)
            {
                count += 1;
                if (!child.isFixedFrame)
                {
                    string filename = exportFiles(child, package, count);
                    child.Visual.Geometry.Mesh.filename = filename;
                    child.Collision.Geometry.Mesh.filename = filename;
                }
            }

            // Copy the texture file (if it was specified) to the textures directory
            if (!Link.isFixedFrame && Link.Visual.Material.Texture.wFilename != "")
            {
                if (System.IO.File.Exists(Link.Visual.Material.Texture.wFilename))
                {
                    Link.Visual.Material.Texture.filename = package.TexturesDirectory + Path.GetFileName(Link.Visual.Material.Texture.wFilename);
                    string textureSavePath = package.WindowsTexturesDirectory + Path.GetFileName(Link.Visual.Material.Texture.wFilename);
                    System.IO.File.Copy(Link.Visual.Material.Texture.wFilename, textureSavePath, true);
                }
            }

            // Create the mesh filenames. SolidWorks likes to use / but that will get messy in filenames so use _ instead
            string linkName = Link.name.Replace('/', '_');
            string meshFileName = package.MeshesDirectory + linkName + ".STL";
            string windowsMeshFileName = package.WindowsMeshesDirectory + linkName + ".STL";

            int errors = 0;
            int warnings = 0;

            // Export STL
            Common.showComponents(ActiveSWModel, Link.SWcomponents);

            int saveOptions = (int)swSaveAsOptions_e.swSaveAsOptions_Silent;
            if (Link.Joint == null || Link.Joint.CoordinateSystemName == null)
            {
                setLinkSpecificSTLPreferences(Link.CoordSysName, Link.STLQualityFine);
            }
            else
            {
                setLinkSpecificSTLPreferences(Link.Joint.CoordinateSystemName, Link.STLQualityFine);
            }
            string coordsysname = ActiveSWModel.Extension.GetUserPreferenceString((int)swUserPreferenceStringValue_e.swFileSaveAsCoordinateSystem, (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified);
            ActiveSWModel.Extension.SaveAs(windowsMeshFileName, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, saveOptions, null, ref errors, ref warnings);
            Common.hideComponents(ActiveSWModel, Link.SWcomponents);

            correctSTLMesh(windowsMeshFileName);

            return meshFileName;
        }

        // Used only by the part exporter
        public void exportLink(bool zIsUp)
        {
            
            createBaseRefOrigin(zIsUp);
            MathTransform coordSysTransform = ActiveSWModel.Extension.GetCoordinateSystemTransformByName("Origin_global");
            Matrix<double> GlobalTransform = ops.getTransformation(coordSysTransform);

            localizeLink(mRobot.BaseLink, GlobalTransform);

            //Creating package directories
            URDFPackage package = new URDFPackage(mPackageName, mSavePath);
            package.createDirectories();
            string meshFileName = package.MeshesDirectory + mRobot.BaseLink.name + ".STL";
            string windowsMeshFileName = package.WindowsMeshesDirectory + mRobot.BaseLink.name + ".STL";
            string windowsURDFFileName = package.WindowsRobotsDirectory + mRobot.name + ".URDF";
            string windowsManifestFileName = package.WindowsPackageDirectory + "manifest.xml";

            //Creating manifest file
            manifestWriter manifestWriter = new manifestWriter(windowsManifestFileName);
            manifest Manifest = new manifest(mRobot.name);
            Manifest.writeElement(manifestWriter);

            //Customizing STL preferences to how I want them
            saveUserPreferences();
            setSTLExportPreferences();
            setLinkSpecificSTLPreferences("", mRobot.BaseLink.STLQualityFine);
            int errors = 0;
            int warnings = 0;

            //Saving part as STL mesh

            ActiveSWModel.Extension.SaveAs(windowsMeshFileName, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
            mRobot.BaseLink.Visual.Geometry.Mesh.filename = meshFileName;
            mRobot.BaseLink.Collision.Geometry.Mesh.filename = meshFileName;

            correctSTLMesh(windowsMeshFileName);

            mRobot.BaseLink.Visual.Material.Texture.filename = package.TexturesDirectory + Path.GetFileName(mRobot.BaseLink.Visual.Material.Texture.wFilename);
            string textureSavePath = package.WindowsTexturesDirectory + Path.GetFileName(mRobot.BaseLink.Visual.Material.Texture.wFilename);
            if (mRobot.BaseLink.Visual.Material.Texture.wFilename != "")
            {
                System.IO.File.Copy(mRobot.BaseLink.Visual.Material.Texture.wFilename, textureSavePath, true);
            }

            //Writing URDF to file
            URDFWriter uWriter = new URDFWriter(windowsURDFFileName);
            //mRobot.addLink(mLink);
            mRobot.writeURDF(uWriter.writer);

            resetUserPreferences();
        }

        //Writes an empty header to the STL to get rid of the BS that SolidWorks adds to a binary STL file
        public void correctSTLMesh(string filename)
        {
            FileStream fileStream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            byte[] emptyHeader = new byte[80];
            fileStream.Write(emptyHeader, 0, emptyHeader.Length);
            fileStream.Close();
        }
        #endregion

        #region STL Preference shuffling

        //Saves the preferences that the user had setup so that I can change them and revert back to their configuration
        public void saveUserPreferences()
        {
            mBinary = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat);
            mTranslateToPositive = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive);
            mSTLUnits = iSwApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits);
            mSTLQuality = iSwApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality);
            mshowInfo = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave);
            mSTLPreview = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview);
            mHideTransitionSpeed = iSwApp.GetUserPreferenceDoubleValue((int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent);
            mSaveComponentsIntoOneFile = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile);
        }

        //This is how the STL export preferences need to be to properly export
        public void setSTLExportPreferences()
        {
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, true);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, 2);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Coarse);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, false);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, false);
            iSwApp.SetUserPreferenceDoubleValue((int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent, 0);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, true);
        }

        //This resets the user preferences back to what they were.
        public void resetUserPreferences()
        {
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, mBinary);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, mTranslateToPositive);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, mSTLUnits);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, mSTLQuality);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, mshowInfo);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, mSTLPreview);
            iSwApp.SetUserPreferenceDoubleValue((int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent, mHideTransitionSpeed);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, mSaveComponentsIntoOneFile);
        }

        //If the user selected something specific for a particular link, that is handled here.
        public void setLinkSpecificSTLPreferences(string CoordinateSystemName, bool qualityFine)
        {
            ActiveSWModel.Extension.SetUserPreferenceString((int)swUserPreferenceStringValue_e.swFileSaveAsCoordinateSystem, (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, CoordinateSystemName);
            if (qualityFine)
            {
                iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Fine);
            }
            else
            {
                iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Coarse);
            }
        }
        #endregion
    }
}