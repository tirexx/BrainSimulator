﻿using GoodAI.BrainSimulator.Utils;
using GoodAI.Core;
using GoodAI.Core.Configuration;
using GoodAI.Core.Execution;
using GoodAI.Core.Memory;
using GoodAI.Core.Utils;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using GoodAI.Platform.Core.Logging;
using WeifenLuo.WinFormsUI.Docking;
using YAXLib;

namespace GoodAI.BrainSimulator.Forms
{
    public partial class MainForm : Form
    {        
        private static Color STATUS_BAR_BLUE = Color.FromArgb(255, 0, 122, 204);
        private static Color STATUS_BAR_BLUE_BUILDING = Color.FromArgb(255, 14, 99, 156);

        private MruStripMenuInline m_recentMenu;
        private bool m_isClosing = false;

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (!TryRestoreViewsLayout(UserLayoutFileName))
            {
                ResetViewsLayout();
            }

            this.WindowState = FormWindowState.Maximized;
            statusStrip.BackColor = STATUS_BAR_BLUE;

            if (!TryOpenStartupProject())
            {
                OpenGraphLayout(Project.Network);
            }

            m_recentMenu = new MruStripMenuInline(fileToolStripMenuItem, recentFilesMenuItem , RecentFiles_Click, 5);

            StringCollection recentFilesList = Properties.Settings.Default.RecentFilesList;

            if (recentFilesList != null)
            {
                string[] tmp = new string[recentFilesList.Count];
                recentFilesList.CopyTo(tmp, 0);
                m_recentMenu.AddFiles(tmp);
            }
        }

        private bool TryOpenStartupProject()
        {
            try
            {
                if (!string.IsNullOrEmpty(MyConfiguration.OpenOnStartupProjectName))
                {
                    OpenProject(MyConfiguration.OpenOnStartupProjectName);
                }
                else if (!string.IsNullOrEmpty(Properties.Settings.Default.LastProject))
                {
                    OpenProject(Properties.Settings.Default.LastProject);
                }
                else
                {
                    return false;
                }
            }
            catch (ProjectLoadingException)  // already logged
            {
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(this.GetType(), "Error setting up startup project: " + ex.Message);
                return false;
            }

            return true;
        }

        //TODO: this should be done by data binding but menu items cannot do that (add this support)
        void SimulationHandler_StateChanged(object sender, MySimulationHandler.StateEventArgs e)
        {
            runToolButton.Enabled = SimulationHandler.CanStart;            
            startToolStripMenuItem.Enabled = SimulationHandler.CanStart;            

            pauseToolButton.Enabled = SimulationHandler.CanPause;
            pauseToolStripMenuItem.Enabled = SimulationHandler.CanPause;

            stopToolButton.Enabled = SimulationHandler.CanStop;
            stopToolStripMenuItem.Enabled = SimulationHandler.CanStop;

            debugToolButton.Enabled = SimulationHandler.CanStartDebugging;
            debugToolStripMenuItem.Enabled = SimulationHandler.CanStartDebugging;

            stepOverToolButton.Enabled = SimulationHandler.CanStepOver;
            stepOverToolStripMenuItem.Enabled = SimulationHandler.CanStepOver;

            stepIntoToolStripMenuItem.Enabled = SimulationHandler.CanStepInto;
            stepOutToolStripMenuItem.Enabled = SimulationHandler.CanStepOut;

            reloadButton.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;

            simStatusLabel.Text = SimulationHandler.State.GetAttributeProperty((DescriptionAttribute x) => x.Description);           

            //TODO: this is awful, binding is needed here for sure            
            newProjectToolButton.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;
            newProjectToolStripMenuItem.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;

            openProjectToolButton.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;
            openProjectToolStripMenuItem.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;

            saveProjectToolButton.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;
            saveProjectAsToolStripMenuItem.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;
            saveProjectToolStripMenuItem.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;

            copySelectionToolStripMenuItem.Enabled = pasteSelectionToolStripMenuItem.Enabled =
                SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;

            worldList.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;

            NodePropertyView.CanEdit = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;

            updateMemoryBlocksToolStripMenuItem.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED;
            
            MemoryBlocksView.Enabled = SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED ||
                 SimulationHandler.State == MySimulationHandler.SimulationState.PAUSED;

            if (SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED)
            {                
                stepStatusLabel.Text = String.Empty;
                statusStrip.BackColor = STATUS_BAR_BLUE;

                exportStateButton.Enabled = MyMemoryBlockSerializer.TempDataExists(Project);
                clearDataButton.Enabled = exportStateButton.Enabled;
            }
            else if (SimulationHandler.State == MySimulationHandler.SimulationState.PAUSED)
            {
                statusStrip.BackColor = Color.Chocolate;
            }
            else
            {
                statusStrip.BackColor = STATUS_BAR_BLUE_BUILDING;
            }
        }    

        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                OpenProjectAndAddToRecentMenu(openFileDialog.FileName);
            }            
        }

        private void OpenProjectAndAddToRecentMenu(string fileName)
        {
            try
            {
                OpenProject(fileName);
                m_recentMenu.AddFile(fileName);
            }
            catch (ProjectLoadingException)
            {
                // already logged
            }
            catch (Exception ex)
            {
                Log.Error(this.GetType(), "Error while opening a project:" + ex.Message);
            }
        }

        private void nodeSettingsMenuItem_Click(object sender, EventArgs e)
        {
            NodeSelectionForm selectionForm = new NodeSelectionForm(this);
            selectionForm.StartPosition = FormStartPosition.CenterParent;

            if (selectionForm.ShowDialog(this) == DialogResult.OK)
            {
                PopulateWorldList();

                foreach (GraphLayoutForm gv in GraphViews.Values)
                {
                    gv.InitToolBar();
                }
            }
        }

        private void viewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DockContent view = (sender as ToolStripMenuItem).Tag as DockContent;
            OpenFloatingOrActivate(view);
        }

        private void OpenFloatingOrActivate(DockContent view)
        {
            if ((view.DockAreas & DockAreas.Float) > 0 && !view.Created)
            {
                Size viewSize = new Size(view.Bounds.Size.Width, view.Bounds.Size.Height);
                view.Show(dockPanel, DockState.Float);
                view.FloatPane.FloatWindow.Size = viewSize;                
            }
            else
            {
                view.Activate();
            }
        }

        public void OpenNodeHelpView()
        {
            OpenFloatingOrActivate(HelpView);            
        }

        private void saveProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveProjectOrSaveAs();
        }

        private void SaveProjectOrSaveAs()
        {
            if (saveFileDialog.FileName != string.Empty)
            {
                SaveProject(saveFileDialog.FileName);
            }
            else
            {
                SaveProjectAs();  // ask for file name and then save the project
            }
        }

        private void saveProjectAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveProjectAs();
        }

        private void SaveProjectAs()
        {
            if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                SaveProject(saveFileDialog.FileName);
                m_recentMenu.AddFile(saveFileDialog.FileName);
            }
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void newProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloseCurrentProjectWindows();
            CreateNewProject();            

            CreateNetworkView();
            OpenGraphLayout(Project.Network);

            Properties.Settings.Default.LastProject = String.Empty;
            saveFileDialog.FileName = String.Empty;
        }

        private void runToolButton_Click(object sender, EventArgs e)
        {
            ShowHideAllObservers(forceShow: true);
            ConsoleView.Activate();            
            StartSimulation(false);            
        }

        private void stepOverToolButton_Click(object sender, EventArgs e)
        {
            ShowHideAllObservers(forceShow: true);
            ConsoleView.Activate();
            StartSimulation(true);            
        }

        private void stopToolButton_Click(object sender, EventArgs e)
        {
            ShowHideAllObservers(forceShow: true);
            SimulationHandler.StopSimulation();
            SimulationHandler.Simulation.InDebugMode = false;
        }

        private void pauseToolButton_Click(object sender, EventArgs e)
        {
            ShowHideAllObservers(forceShow: true);
            SimulationHandler.PauseSimulation();
        }

        private void worldList_SelectedIndexChanged(object sender, EventArgs e)
        {
            CloseObservers(Project.World);

            if (worldList.SelectedItem != null )
            {
                MyWorldConfig wc = worldList.SelectedItem as MyWorldConfig;

                if (Project.World == null || wc.NodeType != Project.World.GetType())
                {
                    Project.CreateWorld(wc.NodeType);
                    Project.World.EnableDefaultTasks();
                    NodePropertyView.Target = null;

                    if (NetworkView != null)
                    {
                        NetworkView.ReloadContent();
                    }

                    foreach (GraphLayoutForm graphView in GraphViews.Values)
                    {
                        graphView.Desktop.Invalidate();                        
                        graphView.worldButton_Click(sender, e);                     
                    }
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_isClosing) return;

            // Cancel the event - the window will close when the simulation is finished.
            e.Cancel = true;
           
            if (SimulationHandler.State != MySimulationHandler.SimulationState.STOPPED)
            {
                DialogResult dialogResult = DialogResult.None;

                PauseSimulationForAction(() =>
                {
                    dialogResult =
                        MessageBox.Show(
                            "Do you want to quit while the simulation is running?",
                            "Quit?",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    return dialogResult == DialogResult.No;                    
                });

                if (dialogResult == DialogResult.No)
                {
                    return;
                }
            }           

            if ((String.IsNullOrEmpty(saveFileDialog.FileName)) || !IsProjectSaved(saveFileDialog.FileName))
            {
                var dialogResult = MessageBox.Show("Save project changes?",
                    "Save Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                // Do not close.
                if (dialogResult == DialogResult.Cancel)
                    return;

                if (dialogResult == DialogResult.Yes)
                    SaveProjectOrSaveAs();
            }

            // When this is true, the event will just return next time it's called.
            m_isClosing = true;
            SimulationHandler.Finish(Close);
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            StoreViewsLayout(UserLayoutFileName);

            Properties.Settings.Default.RecentFilesList = new StringCollection();
            Properties.Settings.Default.RecentFilesList.AddRange(m_recentMenu.GetFiles());

            Properties.Settings.Default.Save();
        }

        private void reloadButton_Click(object sender, EventArgs e)
        {
            MyKernelFactory.Instance.ClearLoadedKernels();
            Log.Info(this.GetType(), "Kernel cache cleared.");
        }

        private void loadUserNodesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openNodeFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                Properties.Settings.Default.UserNodesFile = openNodeFileDialog.FileName;

                if (MessageBox.Show("Restart is needed for this action to take effect.\nDo you want to quit application?", "Restart needed",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    Close();
                }
            } 
        }

        private void importProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                saveFileDialog.FileName = openFileDialog.FileName;
                var dr = MessageBox.Show("Import observers?", "Importing project", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                ImportProject(openFileDialog.FileName, dr == DialogResult.Yes);
            }            
        }

        private void copySelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dockPanel.ActiveContent is TextEditForm)
            {
                (dockPanel.ActiveDocument as TextEditForm).CopyText();
            }
            else if (dockPanel.ActiveContent is ConsoleForm)
            {
                Clipboard.SetText((dockPanel.ActiveContent as ConsoleForm).textBox.SelectedText);
                return;
            }
            else if (dockPanel.ActiveDocument is GraphLayoutForm)
            {
                CopySelectedNodesToClipboard();
            }
        }

        private void pasteSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dockPanel.ActiveContent is TextEditForm)
            {
                (dockPanel.ActiveDocument as TextEditForm).PasteText();
            }            
            else if (dockPanel.ActiveDocument is GraphLayoutForm)
            {
                PasteNodesFromClipboard();
            }            
        }

        private void RecentFiles_Click(int number, string fileName)
        {
            OpenProjectAndAddToRecentMenu(fileName);
        }

        private void setGlobalDataFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openMemFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                string dataFolder = Path.GetDirectoryName(openMemFileDialog.FileName) + "\\" +
                        Path.GetFileNameWithoutExtension(openMemFileDialog.FileName) + ".statedata";

                SimulationHandler.Simulation.GlobalDataFolder = dataFolder;
                setGlobalDataFolderToolStripMenuItem.Text = "Change global data folder: " + SimulationHandler.Simulation.GlobalDataFolder;
                clearGlobalDataFolderToolStripMenuItem.Visible = true;
            }
        }

        private void clearGlobalDataFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Unset the global data folder: " + SimulationHandler.Simulation.GlobalDataFolder + " ?",
                "Unset", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                SimulationHandler.Simulation.GlobalDataFolder = String.Empty;
                setGlobalDataFolderToolStripMenuItem.Text = "Set global data folder";
                clearGlobalDataFolderToolStripMenuItem.Visible = false;
            }
        }

        private void loadOnStartMenuItem_Click(object sender, EventArgs e)
        {
            if (!loadMemBlocksButton.Checked)
            {
                SimulationHandler.Simulation.LoadAllNodesData = true;
                loadMemBlocksButton.Checked = true;
            }
            else
            {
                SimulationHandler.Simulation.LoadAllNodesData = false;
                loadMemBlocksButton.Checked = false;                
            }
        }

        private void saveOnStopMenuItem_CheckChanged(object sender, EventArgs e)
        {
            SimulationHandler.Simulation.SaveAllNodesData = saveMemBlocksButton.Checked;
        }

        private void exportStateButton_Click(object sender, EventArgs e)
        {
            if (saveMemFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    string dataFolder = Path.GetDirectoryName(saveMemFileDialog.FileName) + "\\" +
                        Path.GetFileNameWithoutExtension(saveMemFileDialog.FileName) + ".statedata";
                    
                    MyMemoryBlockSerializer.ExportTempStorage(Project, dataFolder);

                    MyNetworkState networkState = new MyNetworkState()
                    {
                        ProjectName = Project.Name,
                        MemoryBlocksLocation = dataFolder,
                        SimulationStep = SimulationHandler.SimulationStep
                    };

                    YAXSerializer serializer = new YAXSerializer(typeof(MyNetworkState),
                        YAXExceptionHandlingPolicies.ThrowErrorsOnly,
                        YAXExceptionTypes.Warning);

                    serializer.SerializeToFile(networkState, saveMemFileDialog.FileName);
                    Log.Info(this.GetType(), "Saving state: " + saveMemFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    Log.Error(this.GetType(), "Saving state failed: " + ex.Message);
                }    
            }
        }

        private void clearDataButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Clear all temporal data for project: " + Project.Name + "?", 
                "Clear data", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                MyMemoryBlockSerializer.ClearTempStorage(Project);

                exportStateButton.Enabled = false;
                clearDataButton.Enabled = false;
            }
        }

        private void updateMemoryBlocksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SimulationHandler.UpdateMemoryModel();

            foreach (GraphLayoutForm graphView in GraphViews.Values)
            {
                graphView.Desktop.Invalidate();
            }
        }

        private void guideToolStripMenuItem_Click(object sender, EventArgs e) 
        {
            try
            {
                MyDocProvider.Navigate(Properties.Settings.Default.HelpUrl);
            }
            catch (Exception exc)
            {
                Log.Error(this.GetType(), "Failed to get HelpUrl setting: " + exc.Message);
            }
        }

        private void autosaveButton_CheckedChanged(object sender, EventArgs e)
        {
            autosaveTextBox.Enabled = autosaveButton.Checked;
            SimulationHandler.AutosaveEnabled = autosaveButton.Checked;
            Properties.Settings.Default.AutosaveEnabled = autosaveButton.Checked;
        }        

        private void autosaveTextBox_Validating(object sender, CancelEventArgs e)
        {
            int result = 0;

            if (int.TryParse(autosaveTextBox.Text, out result))
            {
                SimulationHandler.AutosaveInterval = result;
                Properties.Settings.Default.AutosaveInterval = result;
            }
            else
            {
                autosaveTextBox.Text = SimulationHandler.AutosaveInterval + "";
            }
        }

        private void debugToolButton_Click(object sender, EventArgs e)
        {            
            SimulationHandler.Simulation.InDebugMode = true;
            StartSimulation(true);

            OpenFloatingOrActivate(DebugView);        
        }

        private void stepIntoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StartSimulation(true);
        }

        private void stepOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StartSimulation(true);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var aboutDialog = new AboutDialog();
            aboutDialog.ShowDialog();
        }

        private bool handleFirstClickOnActivated = false;

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Form.Activated" /> event.
        /// Handle WinForms bug for first click during activation
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> that contains the event data.</param>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (this.handleFirstClickOnActivated)
            {
                var cursorPosition = Cursor.Position;
                var clientPoint = this.PointToClient(cursorPosition);
                var child = this.GetChildAtPoint(clientPoint);

                while (this.handleFirstClickOnActivated && child != null)
                {
                    var toolStrip = child as ToolStrip;
                    if (toolStrip != null)
                    {
                        this.handleFirstClickOnActivated = false;
                        clientPoint = toolStrip.PointToClient(cursorPosition);
                        foreach (var item in toolStrip.Items)
                        {
                            var toolStripItem = item as ToolStripItem;
                            if (toolStripItem != null && toolStripItem.Bounds.Contains(clientPoint))
                            {
                                var tsMenuItem = item as ToolStripDropDownItem;
                                if (tsMenuItem != null)
                                {
                                    tsMenuItem.ShowDropDown();
                                    break;
                                }

                                toolStripItem.PerformClick();
                                break;
                            }
                        }
                    }
                    else
                    {
                        child = child.GetChildAtPoint(clientPoint);
                    }
                }
                this.handleFirstClickOnActivated = false;
            }
        }

        /// <summary>
        /// If the form is being focused (activated), set the handleFirstClickOnActivated flag
        /// indicating that so that it can be later used in OnActivated.
        /// </summary>
        /// <param name="m"></param>
        protected override void WndProc(ref Message m)
        {
            const int WM_ACTIVATE = 0x0006;
            const int WA_CLICKACTIVE = 0x0002;
            if (m.Msg == WM_ACTIVATE && Low16(m.WParam) == WA_CLICKACTIVE)
            {
                handleFirstClickOnActivated = true;
            }
            base.WndProc(ref m);
        }

        private static int GetIntUnchecked(IntPtr value)
        {
            return IntPtr.Size == 8 ? unchecked((int)value.ToInt64()) : value.ToInt32();
        }

        private static int Low16(IntPtr value)
        {
            return unchecked((short)GetIntUnchecked(value));
        }

        private static int High16(IntPtr value)
        {
            return unchecked((short)(((uint)GetIntUnchecked(value)) >> 16));
        }
    }
}