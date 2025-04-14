namespace NetworkDiagnosticTool.UI
{
    partial class RouteTracingForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            
            // Initialize mainTableLayoutPanel
            this.mainTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.mainTableLayoutPanel.ColumnCount = 1;
            this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainTableLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.mainTableLayoutPanel.Margin = new System.Windows.Forms.Padding(0);
            this.mainTableLayoutPanel.Name = "mainTableLayoutPanel";
            this.mainTableLayoutPanel.RowCount = 4;
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.mainTableLayoutPanel.Size = new System.Drawing.Size(834, 561);
            this.mainTableLayoutPanel.TabIndex = 1;
            
            // Initialize topPanel
            this.topPanel = new System.Windows.Forms.TableLayoutPanel();
            this.topPanel.ColumnCount = 2;
            this.topPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70F));
            this.topPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.topPanel.Name = "topPanel";
            this.topPanel.RowCount = 1;
            this.topPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.topPanel.TabIndex = 0;
            
            // Initialize hostComboBox
            this.hostComboBox = new System.Windows.Forms.ComboBox();
            this.hostComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hostComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.hostComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.hostComboBox.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.hostComboBox.ForeColor = System.Drawing.Color.LightGray;
            this.hostComboBox.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.hostComboBox.Name = "hostComboBox";
            
            // Initialize startButton
            this.startButton = new System.Windows.Forms.Button();
            this.startButton.Text = "开始追踪";
            this.startButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.startButton.Click += new System.EventHandler(this.StartButton_Click);
            this.startButton.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.startButton.ForeColor = System.Drawing.Color.LightGray;
            this.startButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.startButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(60, 60, 60);
            this.startButton.Name = "startButton";
            
            // Add controls to topPanel
            this.topPanel.Controls.Add(this.hostComboBox, 0, 0);
            this.topPanel.Controls.Add(this.startButton, 1, 0);
            
            // Initialize buttonPanel
            this.buttonPanel = new System.Windows.Forms.TableLayoutPanel();
            this.buttonPanel.ColumnCount = 2;
            this.buttonPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.buttonPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.buttonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonPanel.Name = "buttonPanel";
            this.buttonPanel.RowCount = 1;
            this.buttonPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.buttonPanel.TabIndex = 2;
            
            // Initialize exportButton
            this.exportButton = new System.Windows.Forms.Button();
            this.exportButton.Text = "导出结果";
            this.exportButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.exportButton.Click += new System.EventHandler(this.ExportButton_Click);
            this.exportButton.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.exportButton.ForeColor = System.Drawing.Color.LightGray;
            this.exportButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.exportButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(60, 60, 60);
            this.exportButton.Name = "exportButton";
            
            // Initialize visualizeButton
            this.visualizeButton = new System.Windows.Forms.Button();
            this.visualizeButton.Text = "可视化结果";
            this.visualizeButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.visualizeButton.Click += new System.EventHandler(this.VisualizeButton_Click);
            this.visualizeButton.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.visualizeButton.ForeColor = System.Drawing.Color.LightGray;
            this.visualizeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.visualizeButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(60, 60, 60);
            this.visualizeButton.Name = "visualizeButton";
            
            // Add controls to buttonPanel
            this.buttonPanel.Controls.Add(this.exportButton, 0, 0);
            this.buttonPanel.Controls.Add(this.visualizeButton, 1, 0);
            
            // Initialize resultListView
            this.resultListView = new System.Windows.Forms.ListView();
            this.resultListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultListView.View = System.Windows.Forms.View.Details;
            this.resultListView.FullRowSelect = true;
            this.resultListView.GridLines = true;
            this.resultListView.BackColor = System.Drawing.Color.FromArgb(35, 35, 35);
            this.resultListView.ForeColor = System.Drawing.Color.LightGray;
            this.resultListView.Name = "resultListView";
            
            // Initialize statusLabel
            this.statusLabel = new System.Windows.Forms.Label();
            this.statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.statusLabel.AutoSize = true;
            this.statusLabel.ForeColor = System.Drawing.Color.LightGray;
            this.statusLabel.Name = "statusLabel";
            
            // Add controls to mainTableLayoutPanel
            this.mainTableLayoutPanel.Controls.Add(this.topPanel, 0, 0);
            this.mainTableLayoutPanel.Controls.Add(this.resultListView, 0, 1);
            this.mainTableLayoutPanel.Controls.Add(this.buttonPanel, 0, 2);
            this.mainTableLayoutPanel.Controls.Add(this.statusLabel, 0, 3);
            
            // Configure form
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Controls.Add(this.mainTableLayoutPanel);
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Text = "路由追踪";
            this.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.ForeColor = System.Drawing.Color.LightGray;
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel mainTableLayoutPanel;
        private System.Windows.Forms.TableLayoutPanel topPanel;
        private System.Windows.Forms.TableLayoutPanel buttonPanel;
        private System.Windows.Forms.ComboBox hostComboBox;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Button visualizeButton;
        private System.Windows.Forms.ListView resultListView;
        private System.Windows.Forms.Label statusLabel;
    }
} 