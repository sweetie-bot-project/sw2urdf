﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

using SolidWorks.Interop.sldworks;

namespace SW2URDF
{
    public partial class PartExportForm : Form
    {
        public SW2URDFExporter Exporter;
        public PartExportForm(ISldWorks iSwApp)
        {
            InitializeComponent();
            Exporter = new SW2URDFExporter(iSwApp);
        }

        #region Basic event handelers
        private void textBox_name_TextChanged(object sender, EventArgs e)
        {

        }

        private void checkBox_static_CheckedChanged(object sender, EventArgs e)
        {

        }
        private void saveFileDialog_save_as_FileOk(object sender, CancelEventArgs e)
        {

        }

        private void textBox_save_as_TextChanged(object sender, EventArgs e)
        {

        }

        private void button_savename_browse_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.RestoreDirectory = true;
            saveFileDialog1.InitialDirectory = Path.GetDirectoryName(textBox_save_as.Text);
            saveFileDialog1.FileName = Path.GetFileName(textBox_save_as.Text);
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox_save_as.Text = saveFileDialog1.FileName;
            }
        }
        #endregion

        #region Inertial event handelers
        private void textBox_inertial_origin_x_TextChanged(object sender, EventArgs e)
        {
        }

        private void textBox_inertial_origin_y_TextChanged(object sender, EventArgs e)
        {
        }

        private void textBox_inertial_origin_z_TextChanged(object sender, EventArgs e)
        {
        }

        private void textBox_inertial_origin_roll_TextChanged(object sender, EventArgs e)
        {
        }

        private void textBox_inertial_origin_pitch_TextChanged(object sender, EventArgs e)
        {
        }

        private void textBox_inertial_origin_yaw_TextChanged(object sender, EventArgs e)
        {
        }

        private void textBox_mass_TextChanged(object sender, EventArgs e)
        {
        }

        private void textBox_ixx_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_ixy_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_ixz_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_iyy_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_iyz_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_izz_TextChanged(object sender, EventArgs e)
        {

        }
        #endregion

        #region Visual event handlers
        private void textBox_visual_origin_x_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_visual_origin_y_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_visual_origin_z_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_visual_origin_roll_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_visual_origin_pitch_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_visual_origin_yaw_TextChanged(object sender, EventArgs e)
        {

        }

        private void trackBar_visual_Scroll(object sender, EventArgs e)
        {

        }

        private void comboBox_materials_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void domainUpDown_red_SelectedItemChanged(object sender, EventArgs e)
        {

        }

        private void domainUpDown_green_SelectedItemChanged(object sender, EventArgs e)
        {

        }

        private void domainUpDown_blue_SelectedItemChanged(object sender, EventArgs e)
        {

        }

        private void domainUpDown_alpha_SelectedItemChanged(object sender, EventArgs e)
        {

        }

        private void textBox_texture_TextChanged(object sender, EventArgs e)
        {

        }

        private void button_texturebrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.InitialDirectory = Path.GetDirectoryName(textBox_save_as.Text);
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox_texture.Text = openFileDialog1.FileName;
            }
        }
        #endregion
        
        #region Collision event handlers
        private void textBox_collision_origin_x_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_collision_origin_y_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_collision_origin_z_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_collision_origin_roll_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_collision_origin_pitch_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_collision_origin_yaw_TextChanged(object sender, EventArgs e)
        {

        }

        private void trackBar_collision_Scroll(object sender, EventArgs e)
        {

        }
        #endregion


        #region Form event handlers
        private void button_finish_Click(object sender, EventArgs e)
        {
            
            double value;
            Exporter.mPackageName = Path.GetFileName(textBox_save_as.Text);
            Exporter.mSavePath = Path.GetDirectoryName(textBox_save_as.Text);
            Exporter.mRobot.BaseLink.name = Exporter.mPackageName;
            Exporter.mRobot.BaseLink.uniqueName = Exporter.mPackageName;
            Exporter.mRobot.BaseLink.Inertial.Origin.X = (Double.TryParse(textBox_inertial_origin_x.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Origin.Y = (Double.TryParse(textBox_inertial_origin_y.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Origin.Z = (Double.TryParse(textBox_inertial_origin_z.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Origin.Roll = (Double.TryParse(textBox_inertial_origin_roll.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Origin.Pitch = (Double.TryParse(textBox_inertial_origin_pitch.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Origin.Yaw = (Double.TryParse(textBox_inertial_origin_yaw.Text, out value)) ? value : 0;

            Exporter.mRobot.BaseLink.Inertial.Inertia.Ixx = (Double.TryParse(textBox_ixx.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Inertia.Ixy = (Double.TryParse(textBox_ixy.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Inertia.Ixz = (Double.TryParse(textBox_ixz.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Inertia.Iyy = (Double.TryParse(textBox_iyy.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Inertia.Iyz = (Double.TryParse(textBox_iyz.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Inertial.Inertia.Izz = (Double.TryParse(textBox_izz.Text, out value)) ? value : 0;

            Exporter.mRobot.BaseLink.Inertial.Mass.Value = (Double.TryParse(textBox_mass.Text, out value)) ? value : 0;


            Exporter.mRobot.BaseLink.Visual.Origin.X = (Double.TryParse(textBox_visual_origin_x.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Visual.Origin.Y = (Double.TryParse(textBox_visual_origin_y.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Visual.Origin.Z = (Double.TryParse(textBox_visual_origin_z.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Visual.Origin.Roll = (Double.TryParse(textBox_visual_origin_roll.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Visual.Origin.Pitch = (Double.TryParse(textBox_visual_origin_pitch.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Visual.Origin.Yaw = (Double.TryParse(textBox_visual_origin_yaw.Text, out value)) ? value : 0;

            Exporter.mRobot.BaseLink.Visual.Material.name = comboBox_materials.Text;
            Exporter.mRobot.BaseLink.Visual.Material.Texture.wFilename = textBox_texture.Text;

            Exporter.mRobot.BaseLink.Visual.Material.Color.Red = (Double.TryParse(domainUpDown_red.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Visual.Material.Color.Green = (Double.TryParse(domainUpDown_green.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Visual.Material.Color.Blue = (Double.TryParse(domainUpDown_blue.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Visual.Material.Color.Alpha = (Double.TryParse(domainUpDown_alpha.Text, out value)) ? value : 0;


            Exporter.mRobot.BaseLink.Collision.Origin.X = (Double.TryParse(textBox_collision_origin_x.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Collision.Origin.Y = (Double.TryParse(textBox_collision_origin_y.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Collision.Origin.Z = (Double.TryParse(textBox_collision_origin_z.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Collision.Origin.Roll = (Double.TryParse(textBox_collision_origin_roll.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Collision.Origin.Pitch = (Double.TryParse(textBox_collision_origin_pitch.Text, out value)) ? value : 0;
            Exporter.mRobot.BaseLink.Collision.Origin.Yaw = (Double.TryParse(textBox_collision_origin_yaw.Text, out value)) ? value : 0;

            Exporter.mRobot.BaseLink.STLQualityFine = radioButton_fine.Checked;

            Exporter.exportLink(checkBox_rotate.Checked);
            this.Close();
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void PartExportForm_Load(object sender, EventArgs e)
        {
            Exporter.createRobotFromActiveModel();
            textBox_save_as.Text = Exporter.mSavePath + "\\" + Exporter.mPackageName;

            textBox_collision_origin_x.Text = Exporter.mRobot.BaseLink.Collision.Origin.X.ToString("G5");
            textBox_collision_origin_y.Text = Exporter.mRobot.BaseLink.Collision.Origin.Y.ToString("G5");
            textBox_collision_origin_z.Text = Exporter.mRobot.BaseLink.Collision.Origin.Z.ToString("G5");
            textBox_collision_origin_roll.Text = "0";
            textBox_collision_origin_pitch.Text = "0";
            textBox_collision_origin_yaw.Text = "0";

            textBox_visual_origin_x.Text = Exporter.mRobot.BaseLink.Visual.Origin.X.ToString("G5");
            textBox_visual_origin_y.Text = Exporter.mRobot.BaseLink.Visual.Origin.Y.ToString("G5");
            textBox_visual_origin_z.Text = Exporter.mRobot.BaseLink.Visual.Origin.Z.ToString("G5");
            textBox_visual_origin_roll.Text = "0";
            textBox_visual_origin_pitch.Text = "0";
            textBox_visual_origin_yaw.Text = "0";

            domainUpDown_red.Text = Exporter.mRobot.BaseLink.Visual.Material.Color.Red.ToString("G5");
            domainUpDown_green.Text = Exporter.mRobot.BaseLink.Visual.Material.Color.Green.ToString("G5");
            domainUpDown_blue.Text = Exporter.mRobot.BaseLink.Visual.Material.Color.Blue.ToString("G5");
            domainUpDown_alpha.Text = Exporter.mRobot.BaseLink.Visual.Material.Color.Alpha.ToString("G5");

            textBox_inertial_origin_x.Text = Exporter.mRobot.BaseLink.Inertial.Origin.X.ToString("G5");
            textBox_inertial_origin_y.Text = Exporter.mRobot.BaseLink.Inertial.Origin.Y.ToString("G5");
            textBox_inertial_origin_z.Text = Exporter.mRobot.BaseLink.Inertial.Origin.Z.ToString("G5");
            textBox_inertial_origin_roll.Text = "0";
            textBox_inertial_origin_pitch.Text = "0";
            textBox_inertial_origin_yaw.Text = "0";

            textBox_mass.Text = Exporter.mRobot.BaseLink.Inertial.Mass.Value.ToString("G5");

            textBox_ixx.Text = Exporter.mRobot.BaseLink.Inertial.Inertia.Ixx.ToString("G5");
            textBox_ixy.Text = Exporter.mRobot.BaseLink.Inertial.Inertia.Ixy.ToString("G5");
            textBox_ixz.Text = Exporter.mRobot.BaseLink.Inertial.Inertia.Ixz.ToString("G5");
            textBox_iyy.Text = Exporter.mRobot.BaseLink.Inertial.Inertia.Iyy.ToString("G5");
            textBox_iyz.Text = Exporter.mRobot.BaseLink.Inertial.Inertia.Iyz.ToString("G5");
            textBox_izz.Text = Exporter.mRobot.BaseLink.Inertial.Inertia.Izz.ToString("G5");

        }
        #endregion

    }
}
