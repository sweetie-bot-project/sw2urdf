﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;

namespace SW2URDF
{

    public partial class AssemblyExportForm : Form
    {
        private StringBuilder NewNodeMap = new StringBuilder(128);
        public SW2URDFExporter Exporter;
        public AssemblyExportForm(ISldWorks iSwApp)
        {
            InitializeComponent();
            Exporter = new SW2URDFExporter(iSwApp);
        }

        //Joint form configuration controls
        private void AssemblyExportForm_Load(object sender, EventArgs e)
        {
            Exporter.createRobotFromActiveModel();
            fillTreeViewFromRobot(Exporter.mRobot, treeView_linktree);
            textBox_save_as.Text = Exporter.mSavePath + "\\" + Exporter.mPackageName;

        }

        private void button_link_next_Click(object sender, EventArgs e)
        {
            treeView_jointtree.Nodes.Clear();
            Exporter.mRobot = createRobotFromLinkTreeView(treeView_linktree);
            Exporter.createJoints();
            fillTreeViewFromRobot(Exporter.mRobot, treeView_jointtree);
            panel_joint.Visible = true;
        }

        private void button_link_cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void button_joint_finish_Click(object sender, EventArgs e)
        {
            LinkNode node = (LinkNode)treeView_jointtree.SelectedNode;
            if (node != null)
            {
                node.Joint = saveJointDataFromPropertyBoxes();
            }
            Exporter.exportRobot();
            this.Close();
        }

        private void button_joint_previous_Click(object sender, EventArgs e)
        {
            treeView_linktree.Nodes.Clear();
            Exporter.mRobot = createRobotFromLinkTreeView(treeView_jointtree);
            fillTreeViewFromRobot(Exporter.mRobot, treeView_linktree);
            panel_joint.Visible = false;
        }

        private void button_joint_cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        //Joint form configuration controls

        private void button_select_Click(object sender, EventArgs e)
        {

        }

        private void button_deselect_Click(object sender, EventArgs e)
        {

        }


        #region Dragging and Dropping Links
        private void treeView_linktree_ItemDrag(object sender, System.Windows.Forms.ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);

        }
        private void treeView_linktree_DragOver(object sender, System.Windows.Forms.DragEventArgs e)
        {
            // Retrieve the client coordinates of the mouse position.
            Point targetPoint = treeView_linktree.PointToClient(new Point(e.X, e.Y));

            // Select the node at the mouse position.
            treeView_linktree.SelectedNode = treeView_linktree.GetNodeAt(targetPoint);
            e.Effect = DragDropEffects.Move;
        }
        private void treeView_linktree_DragEnter(object sender, DragEventArgs e)
        {
            // Retrieve the client coordinates of the mouse position.
            Point targetPoint = treeView_linktree.PointToClient(new Point(e.X, e.Y));

            // Select the node at the mouse position.
            treeView_linktree.SelectedNode = treeView_linktree.GetNodeAt(targetPoint);
            e.Effect = DragDropEffects.Move;
        }
        private void treeView_linktree_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            // Retrieve the client coordinates of the drop location.
            Point targetPoint = treeView_linktree.PointToClient(new Point(e.X, e.Y));

            // Retrieve the node at the drop location.
            LinkNode targetNode = (LinkNode)treeView_linktree.GetNodeAt(targetPoint);

            LinkNode draggedNode;
            // Retrieve the node that was dragged.

            //Retrieve the node/item that was dragged
            if ((LinkNode)e.Data.GetData(typeof(LinkNode)) != null)
            {
                draggedNode = (LinkNode)e.Data.GetData(typeof(LinkNode));
            }
            else if ((LinkItem)e.Data.GetData(typeof(LinkItem)) != null)
            {
                LinkItem item = (LinkItem)e.Data.GetData(typeof(LinkItem));
                draggedNode = LinkItemToLinkNode(item);
                listBox_deleted.Items.Remove(item);
            }
            else
            {
                return;
            }

            // If the node is picked up and dragged back on to itself, please don't crash
            if (draggedNode == targetNode)
            {
                return;
            }

            // If the it was dropped into the box itself, but not onto an actual node
            if (targetNode == null)
            {
                // If for some reason the tree is empty
                if (treeView_linktree.Nodes.Count == 0)
                {
                    draggedNode.Remove();
                    treeView_linktree.Nodes.Add(draggedNode);
                    treeView_linktree.ExpandAll();
                    return;
                }
                else
                {
                    targetNode = (LinkNode)treeView_linktree.TopNode;
                    draggedNode.Remove();
                    targetNode.Nodes.Add(draggedNode);
                    targetNode.ExpandAll();
                    return;
                }
            }
            else
            {
                // If dragging a node closer onto its ancestor do parent swapping kungfu
                if (draggedNode.Level < targetNode.Level)
                {
                    int level_diff = targetNode.Level - draggedNode.Level;
                    LinkNode ancestor_node = targetNode;
                    for (int i = 0; i < level_diff; i++)
                    {
                        //Ascend up the target's node (new parent) parent tree the level difference to get the ancestoral node that is at the same level
                        //as the dragged node (the new child)
                        ancestor_node = (LinkNode)ancestor_node.Parent;
                    }
                    // If the dragged node is in the same line as the target node, then the real kungfu begins
                    if (ancestor_node == draggedNode)
                    {
                        LinkNode newParent = targetNode;
                        LinkNode newChild = draggedNode;
                        LinkNode sameGrandparent = (LinkNode)draggedNode.Parent;
                        newParent.Remove(); // Remove the target node from the tree
                        newChild.Remove();  // Remove the dragged node from the tree
                        newParent.Nodes.Add(newChild); // 
                        if (sameGrandparent == null)
                        {
                            treeView_linktree.Nodes.Add(newParent);
                        }
                        else
                        {
                            sameGrandparent.Nodes.Add(newParent);
                        }
                    }
                }
                draggedNode.Remove();
                targetNode.Nodes.Add(draggedNode);
                targetNode.ExpandAll();
            }
        }
        private void listBox_deleted_DragOver(object sender, System.Windows.Forms.DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }
        private void listBox_deleted_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }
        private void listBox_deleted_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            LinkNode draggedNode = (LinkNode)e.Data.GetData(typeof(LinkNode));
            if (draggedNode != null)
            {
                draggedNode.Remove();
                listBox_deleted.Items.Add(LinkNodeToLinkItem(draggedNode));
            }
        }
        private void listBox_deleted_MouseDown(object sender, MouseEventArgs e)
        {
            this.listBox_deleted.DoDragDrop(this.listBox_deleted.SelectedItem, DragDropEffects.Move);
        }

        private void treeView_linktree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            LinkNode node = (LinkNode)e.Node;
            fillLinkPropertyBoxes(node.Link);
            node.Link.SWComponent.Select(false);
        }

        private void listBox_deleted_SelectedIndexChanged(object sender, EventArgs e)
        {
            LinkItem item = (LinkItem)listBox_deleted.SelectedItem;
            if (item != null)
            {
                fillLinkPropertyBoxes(item.Link);
            }
        }

        public LinkItem LinkNodeToLinkItem(LinkNode node)
        {
            LinkItem item = new LinkItem();
            item.Name = node.Name;
            item.Link = node.Link;
            item.Text = node.Text;
            item.Link.Children.Clear();
            foreach (LinkNode child in node.Nodes)
            {
                item.Link.Children.Add(createLinkFromLinkNode(child));
            }
            return item;
        }
        public LinkNode LinkItemToLinkNode(LinkItem item)
        {
            LinkNode node = new LinkNode();
            node.Name = item.Name;
            node.Link = item.Link;
            node.Text = item.Text;
            foreach(link child in item.Link.Children)
            {
                node.Nodes.Add(createLinkNodeFromLink(child));
            }
            return node;
        }
        #endregion

        #region Robot<->Tree

        public void fillLinkPropertyBoxes(link Link)
        {
            textBox_collision_origin_x.Text = Link.Collision.Origin.X.ToString();
            textBox_collision_origin_y.Text = Link.Collision.Origin.Y.ToString();
            textBox_collision_origin_z.Text = Link.Collision.Origin.Z.ToString();
            textBox_collision_origin_roll.Text = "0";
            textBox_collision_origin_pitch.Text = "0";
            textBox_collision_origin_yaw.Text = "0";

            textBox_visual_origin_x.Text = Link.Visual.Origin.X.ToString();
            textBox_visual_origin_y.Text = Link.Visual.Origin.Y.ToString();
            textBox_visual_origin_z.Text = Link.Visual.Origin.Z.ToString();
            textBox_visual_origin_roll.Text = "0";
            textBox_visual_origin_pitch.Text = "0";
            textBox_visual_origin_yaw.Text = "0";

            textBox_inertial_origin_x.Text = Link.Inertial.Origin.X.ToString();
            textBox_inertial_origin_y.Text = Link.Inertial.Origin.Y.ToString();
            textBox_inertial_origin_z.Text = Link.Inertial.Origin.Z.ToString();
            textBox_inertial_origin_roll.Text = "0";
            textBox_inertial_origin_pitch.Text = "0";
            textBox_inertial_origin_yaw.Text = "0";

            textBox_mass.Text = Link.Inertial.Mass.Value.ToString();

            textBox_ixx.Text = Link.Inertial.Inertia.Ixx.ToString();
            textBox_ixy.Text = Link.Inertial.Inertia.Ixy.ToString();
            textBox_ixz.Text = Link.Inertial.Inertia.Ixz.ToString();
            textBox_iyy.Text = Link.Inertial.Inertia.Iyy.ToString();
            textBox_iyz.Text = Link.Inertial.Inertia.Iyz.ToString();
            textBox_izz.Text = Link.Inertial.Inertia.Izz.ToString();

            domainUpDown_red.Text = Link.Visual.Material.Color.Red.ToString();
            domainUpDown_green.Text = Link.Visual.Material.Color.Green.ToString();
            domainUpDown_blue.Text = Link.Visual.Material.Color.Blue.ToString();
            domainUpDown_alpha.Text = Link.Visual.Material.Color.Alpha.ToString();
        }
        public void fillJointPropertyBoxes(joint Joint)
        {
            if (Joint == null)
            {
                textBox_joint_name.Text = "";
                comboBox_joint_type.Text = "";

                textBox_joint_x.Text = "";
                textBox_joint_y.Text = "";
                textBox_joint_z.Text = "";
                textBox_joint_roll.Text = "";
                textBox_joint_pitch.Text = "";
                textBox_joint_yaw.Text = "";

                textBox_axis_x.Text = "";
                textBox_axis_y.Text = "";
                textBox_axis_z.Text = "";

                textBox_limit_lower.Text = "";
                textBox_limit_upper.Text = "";
                textBox_limit_effort.Text = "";
                textBox_limit_velocity.Text = "";

                textBox_calibration_rising.Text = "";
                textBox_calibration_falling.Text = "";

                textBox_friction.Text = "";
                textBox_damping.Text = "";

                textBox_soft_lower.Text = "";
                textBox_soft_upper.Text = "";
                textBox_k_position.Text = "";
                textBox_k_velocity.Text = "";
            }
            else
            {
                textBox_joint_name.Text = Joint.name;
                comboBox_joint_type.Text = Joint.type;

                textBox_joint_x.Text = Joint.Origin.X.ToString();
                textBox_joint_y.Text = Joint.Origin.Y.ToString();
                textBox_joint_z.Text = Joint.Origin.Z.ToString();
                textBox_joint_roll.Text = Joint.Origin.Roll.ToString();
                textBox_joint_pitch.Text = Joint.Origin.Pitch.ToString();
                textBox_joint_yaw.Text = Joint.Origin.Yaw.ToString();

                textBox_axis_x.Text = Joint.Axis.X.ToString();
                textBox_axis_y.Text = Joint.Axis.Y.ToString();
                textBox_axis_z.Text = Joint.Axis.Z.ToString();

                textBox_limit_lower.Text = Joint.Limit.lower.ToString();
                textBox_limit_upper.Text = Joint.Limit.upper.ToString();
                textBox_limit_effort.Text = Joint.Limit.effort.ToString();
                textBox_limit_velocity.Text = Joint.Limit.effort.ToString();

                textBox_calibration_rising.Text = Joint.Calibration.rising.ToString();
                textBox_calibration_falling.Text = Joint.Calibration.falling.ToString();

                textBox_friction.Text = Joint.Dynamics.friction.ToString();
                textBox_damping.Text = Joint.Dynamics.damping.ToString();

                textBox_soft_lower.Text = Joint.Safety.soft_lower.ToString();
                textBox_soft_upper.Text = Joint.Safety.soft_upper.ToString();
                textBox_k_position.Text = Joint.Safety.k_position.ToString();
                textBox_k_velocity.Text = Joint.Safety.k_velocity.ToString();
            }
        }
        public void saveLinkItemData(int index)
        {
            LinkItem item = (LinkItem)listBox_deleted.Items[index];
            item.Link = saveLinkDataFromPropertyBoxes(item.Link);
            listBox_deleted.Items[index] = item;
        }
        public void saveLinkNodeData(int index)
        {
            LinkNode node = (LinkNode)treeView_linktree.Nodes[index];
            node.Link = saveLinkDataFromPropertyBoxes(node.Link);
            treeView_linktree.Nodes[index] = node;
        }
        public link saveLinkDataFromPropertyBoxes(link Link)
        {
            double value;
            Link.Inertial.Origin.X = (Double.TryParse(textBox_inertial_origin_x.Text, out value)) ? value : 0;
            Link.Inertial.Origin.Y = (Double.TryParse(textBox_inertial_origin_y.Text, out value)) ? value : 0;
            Link.Inertial.Origin.Z = (Double.TryParse(textBox_inertial_origin_z.Text, out value)) ? value : 0;
            Link.Inertial.Origin.Roll = (Double.TryParse(textBox_inertial_origin_roll.Text, out value)) ? value : 0;
            Link.Inertial.Origin.Pitch = (Double.TryParse(textBox_inertial_origin_pitch.Text, out value)) ? value : 0;
            Link.Inertial.Origin.Yaw = (Double.TryParse(textBox_inertial_origin_yaw.Text, out value)) ? value : 0;

            Link.Visual.Origin.X = (Double.TryParse(textBox_visual_origin_x.Text, out value)) ? value : 0;
            Link.Visual.Origin.Y = (Double.TryParse(textBox_visual_origin_y.Text, out value)) ? value : 0;
            Link.Visual.Origin.Z = (Double.TryParse(textBox_visual_origin_z.Text, out value)) ? value : 0;
            Link.Visual.Origin.Roll = (Double.TryParse(textBox_visual_origin_roll.Text, out value)) ? value : 0;
            Link.Visual.Origin.Pitch = (Double.TryParse(textBox_visual_origin_pitch.Text, out value)) ? value : 0;
            Link.Visual.Origin.Yaw = (Double.TryParse(textBox_visual_origin_yaw.Text, out value)) ? value : 0;

            Link.Collision.Origin.X = (Double.TryParse(textBox_collision_origin_x.Text, out value)) ? value : 0;
            Link.Collision.Origin.Y = (Double.TryParse(textBox_collision_origin_y.Text, out value)) ? value : 0;
            Link.Collision.Origin.Z = (Double.TryParse(textBox_collision_origin_z.Text, out value)) ? value : 0;
            Link.Collision.Origin.Roll = (Double.TryParse(textBox_collision_origin_roll.Text, out value)) ? value : 0;
            Link.Collision.Origin.Pitch = (Double.TryParse(textBox_collision_origin_pitch.Text, out value)) ? value : 0;
            Link.Collision.Origin.Yaw = (Double.TryParse(textBox_collision_origin_yaw.Text, out value)) ? value : 0;

            Link.Inertial.Mass.Value = (Double.TryParse(textBox_mass.Text, out value)) ? value : 0;

            Link.Inertial.Inertia.Ixx = (Double.TryParse(textBox_ixx.Text, out value)) ? value : 0;
            Link.Inertial.Inertia.Ixy = (Double.TryParse(textBox_ixy.Text, out value)) ? value : 0;
            Link.Inertial.Inertia.Ixz = (Double.TryParse(textBox_ixz.Text, out value)) ? value : 0;
            Link.Inertial.Inertia.Iyy = (Double.TryParse(textBox_iyy.Text, out value)) ? value : 0;
            Link.Inertial.Inertia.Iyz = (Double.TryParse(textBox_iyz.Text, out value)) ? value : 0;
            Link.Inertial.Inertia.Izz = (Double.TryParse(textBox_izz.Text, out value)) ? value : 0;

            Link.Visual.Material.name = comboBox_materials.Text;
            Link.Visual.Material.Texture.filename = textBox_texture.Text;

            Link.Visual.Material.Color.Red = (Double.TryParse(domainUpDown_red.Text, out value)) ? value : 0;
            Link.Visual.Material.Color.Green = (Double.TryParse(domainUpDown_green.Text, out value)) ? value : 0;
            Link.Visual.Material.Color.Blue = (Double.TryParse(domainUpDown_blue.Text, out value)) ? value : 0;
            Link.Visual.Material.Color.Alpha = (Double.TryParse(domainUpDown_alpha.Text, out value)) ? value : 0;

            return Link;
        }
        public void fillTreeViewFromRobot(robot robot, TreeView tree)
        {
            LinkNode baseNode = new LinkNode();
            link baseLink = robot.BaseLink;
            baseNode.Name = baseLink.name;
            baseNode.Text = baseLink.name;
            baseNode.Link = baseLink;
            foreach (link child in baseLink.Children)
            {
                baseNode.Nodes.Add(createLinkNodeFromLink(child));
            }
            tree.Nodes.Add(baseNode);
            tree.ExpandAll();
        }

        public LinkNode createLinkNodeFromLink(link Link)
        {
            LinkNode node = new LinkNode();
            node.Name = Link.name;
            node.Text = Link.name;
            node.Link = Link;
            foreach (link child in Link.Children)
            {
                node.Nodes.Add(createLinkNodeFromLink(child));
            }
            node.Link.Children.Clear(); // Need to erase the children from the embedded link because they may be rearranged later.
            return node;
        }
        public robot createRobotFromLinkTreeView(TreeView tree)
        {
            robot Robot = new robot();
            
            foreach (LinkNode node in tree.Nodes)
            {
                if (node.Level == 0)
                {
                    link BaseLink = createLinkFromLinkNode(node);
                    if (BaseLink != null)
                    {
                        Robot.BaseLink = BaseLink;
                    }
                }
            }
            Robot.name = Exporter.mRobot.name;
            return Robot;
        }

        public link createLinkFromLinkNode(LinkNode node)
        {
            if (node.Checked)
            {
                link Link = node.Link;
                Link.Children.Clear();
                foreach (LinkNode child in node.Nodes)
                {
                    link childLink = createLinkFromLinkNode(child);
                    if (childLink != null)
                    {
                        Link.Children.Add(childLink); // Recreates the children of each embedded link
                    }
                }
                return Link;
            }
            else
            {
                return null;
            }
        }

        public joint saveJointDataFromPropertyBoxes()
        {
            joint Joint = new joint();
            double value = 0;

            Exporter.mRobot.BaseLink.Inertial.Origin.X = (Double.TryParse(textBox_inertial_origin_x.Text, out value)) ? value : 0;
            Joint.name = textBox_joint_name.Text;
            Joint.type = comboBox_joint_type.Text;

            Joint.Origin.X = (Double.TryParse(textBox_joint_x.Text, out value)) ? value : 0;
            Joint.Origin.Y = (Double.TryParse(textBox_joint_y.Text, out value)) ? value : 0;
            Joint.Origin.Z = (Double.TryParse(textBox_joint_z.Text, out value)) ? value : 0;
            Joint.Origin.Roll = (Double.TryParse(textBox_joint_roll.Text, out value)) ? value : 0;
            Joint.Origin.Pitch = (Double.TryParse(textBox_joint_pitch.Text, out value)) ? value : 0;
            Joint.Origin.Yaw = (Double.TryParse(textBox_joint_yaw.Text, out value)) ? value : 0;

            Joint.Axis.X = (Double.TryParse(textBox_axis_x.Text, out value)) ? value : 0;
            Joint.Axis.Y = (Double.TryParse(textBox_axis_y.Text, out value)) ? value : 0;
            Joint.Axis.Z = (Double.TryParse(textBox_axis_z.Text, out value)) ? value : 0;

            Joint.Limit.lower = (Double.TryParse(textBox_limit_lower.Text, out value)) ? value : 0;
            Joint.Limit.upper = (Double.TryParse(textBox_limit_upper.Text, out value)) ? value : 0;
            Joint.Limit.effort = (Double.TryParse(textBox_limit_effort.Text, out value)) ? value : 0;
            Joint.Limit.velocity = (Double.TryParse(textBox_limit_velocity.Text, out value)) ? value : 0;

            Joint.Calibration.rising = (Double.TryParse(textBox_calibration_rising.Text, out value)) ? value : 0;
            Joint.Calibration.falling = (Double.TryParse(textBox_calibration_falling.Text, out value)) ? value : 0;

            Joint.Dynamics.friction = (Double.TryParse(textBox_friction.Text, out value)) ? value : 0;
            Joint.Dynamics.damping = (Double.TryParse(textBox_damping.Text, out value)) ? value : 0;

            Joint.Safety.soft_lower = (Double.TryParse(textBox_soft_lower.Text, out value)) ? value : 0;
            Joint.Safety.soft_upper = (Double.TryParse(textBox_soft_upper.Text, out value)) ? value : 0;
            Joint.Safety.k_position = (Double.TryParse(textBox_k_position.Text, out value)) ? value : 0;
            Joint.Safety.k_velocity = (Double.TryParse(textBox_k_velocity.Text, out value)) ? value : 0;

            return Joint;
        }
        #endregion

        #region Link Properties Controls

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

        private void textBox_mass_TextChanged(object sender, EventArgs e)
        {

        }

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

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
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
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.RestoreDirectory = true;
            saveFileDialog1.InitialDirectory = Path.GetDirectoryName(textBox_save_as.Text);
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox_texture.Text = saveFileDialog1.FileName;
            }
        }

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

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void textBox_name_TextChanged(object sender, EventArgs e)
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

        #region joint property controls
        private void label_damping_Click(object sender, EventArgs e)
        {

        }

        private void treeView_jointtree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            LinkNode node = (LinkNode)e.Node;
            fillJointPropertyBoxes(node.Link.Joint);
        }

        private void textBox_joint_name_TextChanged(object sender, EventArgs e)
        {

        }

        private void comboBox_joint_type_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void textBox_joint_x_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_joint_roll_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_joint_y_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_joint_z_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_joint_pitch_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_joint_yaw_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_axis_x_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_axis_y_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_axis_z_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_limit_lower_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_limit_upper_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_limit_effort_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_limit_velocity_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_calibration_rising_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_calibration_falling_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_friction_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_damping_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_soft_lower_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_soft_upper_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_k_position_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox1_k_velocity_TextChanged(object sender, EventArgs e)
        {

        }
        #endregion

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }

    #region Derived classes
    public class LinkNode : TreeNode
    {
        public link Link
        { get; set; }
        public joint Joint
        { get; set; }
    }
    public class LinkItem : ListViewItem
    {
        public link Link
        { get; set; }
    }
    #endregion
}