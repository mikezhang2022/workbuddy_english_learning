#nullable enable
namespace CSharpVisionDemo;

partial class Form1
{
    private PictureBox pictureBox = null!;
    private Panel bottomPanel = null!;
    private Button btnStart = null!;
    private Button btnStop = null!;
    private Label lblStatus = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            pictureBox.Image?.Dispose();
            pictureBox.Image = null;
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        pictureBox = new PictureBox();
        bottomPanel = new Panel();
        btnStart = new Button();
        btnStop = new Button();
        lblStatus = new Label();
        ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
        bottomPanel.SuspendLayout();
        SuspendLayout();

        pictureBox.BackColor = Color.Black;
        pictureBox.Dock = DockStyle.Fill;
        pictureBox.Name = "pictureBox";
        pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox.TabIndex = 0;
        pictureBox.TabStop = false;

        bottomPanel.Controls.Add(btnStart);
        bottomPanel.Controls.Add(btnStop);
        bottomPanel.Controls.Add(lblStatus);
        bottomPanel.Dock = DockStyle.Bottom;
        bottomPanel.Height = 56;
        bottomPanel.Name = "bottomPanel";
        bottomPanel.Padding = new Padding(10, 8, 10, 8);

        btnStart.Location = new Point(12, 12);
        btnStart.Name = "btnStart";
        btnStart.Size = new Size(96, 32);
        btnStart.Text = "Start";
        btnStart.UseVisualStyleBackColor = true;
        btnStart.Click += BtnStart_Click;

        btnStop.Enabled = false;
        btnStop.Location = new Point(118, 12);
        btnStop.Name = "btnStop";
        btnStop.Size = new Size(96, 32);
        btnStop.Text = "Stop";
        btnStop.UseVisualStyleBackColor = true;
        btnStop.Click += BtnStop_Click;

        lblStatus.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        lblStatus.AutoEllipsis = true;
        lblStatus.Location = new Point(230, 16);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(Width - 250, 24);
        lblStatus.Text = "就绪。请放置 models/ 下的模型文件后点击 Start。";
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(960, 720);
        Controls.Add(pictureBox);
        Controls.Add(bottomPanel);
        MinimumSize = new Size(640, 480);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "CSharpVisionDemo — YOLOv8 + MediaPipe Pose/Hands";
        FormClosing += Form1_FormClosing;
        ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
        bottomPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
