namespace StarWarsApi
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private Button btnLoad;
        private Label lblStatus;
        private SplitContainer splitMain;
        private ListView listStarships;
        private ColumnHeader colName;
        private ColumnHeader colLength;
        private ColumnHeader colPilots;
        private TextBox txtOutput;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            btnLoad = new Button();
            lblStatus = new Label();
            splitMain = new SplitContainer();
            listStarships = new ListView();
            colName = new ColumnHeader();
            colLength = new ColumnHeader();
            colPilots = new ColumnHeader();
            txtOutput = new TextBox();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            SuspendLayout();
            //
            // btnLoad
            //
            btnLoad.Location = new Point(12, 12);
            btnLoad.Name = "btnLoad";
            btnLoad.Size = new Size(180, 32);
            btnLoad.TabIndex = 0;
            btnLoad.Text = "Load starships";
            btnLoad.UseVisualStyleBackColor = true;
            btnLoad.Click += btnLoad_Click;
            //
            // lblStatus
            //
            lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblStatus.Location = new Point(210, 18);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(662, 20);
            lblStatus.TabIndex = 1;
            lblStatus.Text = "Ready — starships with length ≥ 10 and their pilots";
            //
            // splitMain
            //
            splitMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            splitMain.Location = new Point(12, 56);
            splitMain.Name = "splitMain";
            splitMain.Orientation = Orientation.Horizontal;
            //
            // splitMain.Panel1
            //
            splitMain.Panel1.Controls.Add(listStarships);
            //
            // splitMain.Panel2
            //
            splitMain.Panel2.Controls.Add(txtOutput);
            splitMain.Size = new Size(860, 493);
            splitMain.SplitterDistance = 280;
            splitMain.TabIndex = 2;
            //
            // listStarships
            //
            listStarships.Columns.AddRange(new ColumnHeader[] { colName, colLength, colPilots });
            listStarships.Dock = DockStyle.Fill;
            listStarships.FullRowSelect = true;
            listStarships.GridLines = true;
            listStarships.Name = "listStarships";
            listStarships.TabIndex = 0;
            listStarships.UseCompatibleStateImageBehavior = false;
            listStarships.View = View.Details;
            //
            // colName
            //
            colName.Text = "Name";
            colName.Width = 260;
            //
            // colLength
            //
            colLength.Text = "Length";
            colLength.Width = 100;
            //
            // colPilots
            //
            colPilots.Text = "Pilots";
            colPilots.Width = 460;
            //
            // txtOutput
            //
            txtOutput.Dock = DockStyle.Fill;
            txtOutput.Font = new Font("Consolas", 10F);
            txtOutput.Multiline = true;
            txtOutput.Name = "txtOutput";
            txtOutput.ReadOnly = true;
            txtOutput.ScrollBars = ScrollBars.Both;
            txtOutput.TabIndex = 0;
            txtOutput.WordWrap = false;
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(884, 561);
            Controls.Add(splitMain);
            Controls.Add(lblStatus);
            Controls.Add(btnLoad);
            MinimumSize = new Size(640, 400);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Star Wars Starships (SWAPI)";
            Load += Form1_Load;
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            splitMain.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
