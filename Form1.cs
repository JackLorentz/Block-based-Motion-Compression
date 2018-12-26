using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static MyVideoPlayer.Block_Based_Motion_Compression;

namespace MyVideoPlayer
{
    public partial class Form1 : Form
    {
        //元件
        private Graphics target_block, candidate_block;
        //splashscreen
        public event EventHandler LoadCompleted;
        //播放系統
        private List<Bitmap> frames = new List<Bitmap>();
        private List<Bitmap> decoding_frames = new List<Bitmap>();
        private bool is_pause = false;
        private int curr_i = 1, prev_i = 0;
        //影片壓縮
        private Block_Based_Motion_Compression BBMC;
        private IntraFrame_Compression IFC;
        private Interframe_Sub_sampling_Compression ISsC;
        private SaveFileDialog saveFileDialog;
        private byte[] encoded_first_frame;
        //解壓縮
        private byte[] film_array;
        private bool is_mvc = false;//Block-based Motion Compression
        private bool is_ifc = false;//Intraframe Compresion
        private bool is_interfc = false;//Interframe Block-based Difference Compression
        private bool is_interfdc = false;//Interframe Difference Compression
        private List<Bitmap_Motion_Vector> BMVs = new List<Bitmap_Motion_Vector>();//所有frames的motion vector
        private Graphics motion_vector_table;
        private double[] psnr;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            System.Threading.Thread.Sleep(3000);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.OnLoadCompleted(EventArgs.Empty);
        }

        protected virtual void OnLoadCompleted(EventArgs e)
        {
            var handler = LoadCompleted;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //清空
            this.toolStripStatusLabel1.Text = "File Path: None";
            this.is_mvc = false;
            this.is_ifc = false;
            this.is_interfc = false;
            this.saveFileInterframeCompressionToolStripMenuItem.Enabled = false;
            this.saveFileToolStripMenuItem.Enabled = false;
            this.pictureBox1.Image = null;
            this.pictureBox2.Image = null;
            this.pictureBox3.Image = null;
            this.pictureBox4.Image = null;
            this.pictureBox5.Visible = false;
            this.button1.Visible = false;
            this.button2.Visible = false;
            this.button3.Visible = false;
            this.button4.Visible = false;
            this.button5.Visible = false;
            this.button6.Visible = false;
            this.button7.Visible = false;
            this.label1.Visible = false;
            this.label2.Visible = false;
            this.label3.Visible = false;
            this.label4.Visible = false;
            this.label5.Visible = false;
            this.label6.Visible = false;
            this.label1.Text = "PREVIOUS:";
            this.label2.Text = "CURRENT:";
            this.chart1.Visible = false;
            this.chart1.Series.Clear();
            this.chart1.ChartAreas.Clear();
            frames.Clear();
            decoding_frames.Clear();
            BMVs.Clear();
            //初始化資料
            System.IO.Stream stream;
            byte[] array;
            motion_vector_table = this.pictureBox5.CreateGraphics();
            //開檔
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "選擇檔案";
            dialog.InitialDirectory = ".\\";
            dialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.pcx, *.hpcx, *.tiff, *.mvc, *.intrafcp, *.interfcp, *.interfdcp) | *.jpg; *.jpeg; *.png; *.pcx; *.hpcx; *.tiff; *.mvc; *.intrafcp; *.interfcp; *.interfdcp";
            dialog.FilterIndex = 2;
            dialog.RestoreDirectory = true;
            //允許選取多檔案
            dialog.Multiselect = true; 
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if ((stream = dialog.OpenFile()) != null)
                {
                    foreach (string filename in dialog.FileNames)
                    {
                        //用來快速壓縮第一幀
                        if (filename.Contains(".hpcx"))
                        {
                            Huffman huffman = new Huffman();
                            encoded_first_frame = System.IO.File.ReadAllBytes(filename);
                            Bitmap image = huffman.Decoding(encoded_first_frame);
                            frames.Add(image);
                            this.toolStripStatusLabel1.Text = "File Path: " + filename;
                        }
                        else if (filename.Contains(".intrafcp"))
                        {
                            film_array = System.IO.File.ReadAllBytes(filename);
                            is_ifc = true;
                            this.chart1.Visible = true;
                            this.label6.Visible = true;
                            this.toolStripStatusLabel1.Text = "File Path: " + filename;
                        }
                        else if (filename.Contains(".interfcp"))
                        {
                            film_array = System.IO.File.ReadAllBytes(filename);
                            is_interfc = true;
                            this.chart1.Visible = true;
                            this.label6.Visible = true;
                            this.toolStripStatusLabel1.Text = "File Path: " + filename;
                        }
                        else if (filename.Contains(".interfdcp")){
                            film_array = System.IO.File.ReadAllBytes(filename);
                            is_interfdc = true;
                            this.chart1.Visible = true;
                            this.label6.Visible = true;
                            this.toolStripStatusLabel1.Text = "File Path: " + filename;
                        }
                        else if (filename.Contains(".mvc"))
                        {
                            film_array = System.IO.File.ReadAllBytes(filename);
                            is_mvc = true;
                            this.pictureBox5.Visible = true;
                            this.chart1.Visible = true;
                            this.label5.Visible = true;
                            this.label6.Visible = true;
                            this.toolStripStatusLabel1.Text = "File Path: " + filename;
                        }
                        else
                        {
                            Bitmap image = (Bitmap)Image.FromFile(filename);
                            frames.Add(image);
                            this.toolStripStatusLabel1.Text = "File Path: " + filename;
                        }
                    }
                    stream.Close();

                    this.button1.Visible = true;
                    this.button2.Visible = true;
                    this.button3.Visible = true;
                    this.button4.Visible = true;
                    this.button5.Visible = true;
                    this.button6.Visible = true;
                    this.button7.Visible = true;
                    this.button1.Enabled = true;
                    this.button2.Enabled = true;
                    this.button3.Enabled = true;
                    this.button4.Enabled = true;
                    this.button5.Enabled = true;
                    this.button6.Enabled = true;
                    this.button7.Enabled = true;
                    this.label1.Visible = true;
                    this.label2.Visible = true;
                    this.saveFileInterframeCompressionToolStripMenuItem.Enabled = true;
                    this.saveFileToolStripMenuItem.Enabled = true;

                    if (is_mvc)
                    {
                        dialog.Title = "選擇原始檔案(比較PSNR)";
                        dialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.pcx, *.hpcx, *.tiff) | *.jpg; *.jpeg; *.png; *.pcx; *.hpcx; *.tiff";
                        if(dialog.ShowDialog() == DialogResult.OK)
                        {
                            if((stream = dialog.OpenFile()) != null)
                            {
                                foreach (string filename in dialog.FileNames)
                                {
                                    if (filename.Contains(".hpcx"))
                                    {
                                        Huffman huffman = new Huffman();
                                        array = System.IO.File.ReadAllBytes(filename);
                                        Bitmap image = huffman.Decoding(array);
                                        frames.Add(image);
                                    }
                                    else
                                    {
                                        Bitmap image = (Bitmap)Image.FromFile(filename);
                                        frames.Add(image);
                                    }
                                }
                                stream.Close();
                            }
                        }
                        //設定圖表
                        this.chart1.BackColor = Color.LightGray;
                        this.chart1.Series.Add("PSNR");
                        this.chart1.ChartAreas.Add("PSNR");
                        this.chart1.ChartAreas["PSNR"].AxisY.Minimum = 0;
                        this.chart1.ChartAreas["PSNR"].AxisY.Maximum = 4000;
                        this.chart1.ChartAreas["PSNR"].AxisX.Interval = 2;
                        this.chart1.ChartAreas["PSNR"].AxisX.MajorGrid.LineColor = Color.Silver;
                        this.chart1.ChartAreas["PSNR"].AxisY.MajorGrid.LineColor = Color.Silver;
                        this.chart1.ChartAreas["PSNR"].BackColor = Color.DimGray;
                        this.chart1.Series["PSNR"].ChartType = SeriesChartType.Line;
                        this.chart1.Series["PSNR"].Color = Color.Aqua;
                        this.chart1.Series["PSNR"].ChartArea = "PSNR";
                        //開始解壓縮
                        Thread decodingTask = new Thread(new ThreadStart(decodingThread));
                        decodingTask.Start();
                    }
                    else if (is_ifc)
                    {
                        dialog.Title = "選擇原始檔案(比較PSNR)";
                        dialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.pcx, *.hpcx, *.tiff) | *.jpg; *.jpeg; *.png; *.pcx; *.hpcx; *.tiff";
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            if ((stream = dialog.OpenFile()) != null)
                            {
                                foreach (string filename in dialog.FileNames)
                                {
                                    if (filename.Contains(".hpcx"))
                                    {
                                        Huffman huffman = new Huffman();
                                        array = System.IO.File.ReadAllBytes(filename);
                                        Bitmap image = huffman.Decoding(array);
                                        frames.Add(image);
                                    }
                                    else
                                    {
                                        Bitmap image = (Bitmap)Image.FromFile(filename);
                                        frames.Add(image);
                                    }
                                }
                                stream.Close();
                            }
                        }
                        //設定圖表
                        this.chart1.BackColor = Color.LightGray;
                        this.chart1.Series.Add("PSNR");
                        this.chart1.ChartAreas.Add("PSNR");
                        this.chart1.ChartAreas["PSNR"].AxisY.Minimum = 0;
                        this.chart1.ChartAreas["PSNR"].AxisY.Maximum = 40;
                        this.chart1.ChartAreas["PSNR"].AxisX.Interval = 2;
                        this.chart1.ChartAreas["PSNR"].AxisX.MajorGrid.LineColor = Color.Silver;
                        this.chart1.ChartAreas["PSNR"].AxisY.MajorGrid.LineColor = Color.Silver;
                        this.chart1.ChartAreas["PSNR"].BackColor = Color.DimGray;
                        this.chart1.Series["PSNR"].ChartType = SeriesChartType.Line;
                        this.chart1.Series["PSNR"].Color = Color.Aqua;
                        this.chart1.Series["PSNR"].ChartArea = "PSNR";
                        //開始解壓縮
                        Thread decodingTask = new Thread(new ThreadStart(intraDecompressionThread));
                        decodingTask.Start();
                    }
                    else if (is_interfc)
                    {
                        dialog.Title = "選擇原始檔案(比較PSNR)";
                        dialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.pcx, *.hpcx, *.tiff) | *.jpg; *.jpeg; *.png; *.pcx; *.hpcx; *.tiff";
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            if ((stream = dialog.OpenFile()) != null)
                            {
                                foreach (string filename in dialog.FileNames)
                                {
                                    if (filename.Contains(".hpcx"))
                                    {
                                        Huffman huffman = new Huffman();
                                        array = System.IO.File.ReadAllBytes(filename);
                                        Bitmap image = huffman.Decoding(array);
                                        frames.Add(image);
                                    }
                                    else
                                    {
                                        Bitmap image = (Bitmap)Image.FromFile(filename);
                                        frames.Add(image);
                                    }
                                }
                                stream.Close();
                            }
                        }
                        //設定圖表
                        this.chart1.BackColor = Color.LightGray;
                        this.chart1.Series.Add("PSNR");
                        this.chart1.ChartAreas.Add("PSNR");
                        this.chart1.ChartAreas["PSNR"].AxisY.Minimum = 0;
                        this.chart1.ChartAreas["PSNR"].AxisY.Maximum = 1600;
                        this.chart1.ChartAreas["PSNR"].AxisX.Interval = 2;
                        this.chart1.ChartAreas["PSNR"].AxisX.MajorGrid.LineColor = Color.Silver;
                        this.chart1.ChartAreas["PSNR"].AxisY.MajorGrid.LineColor = Color.Silver;
                        this.chart1.ChartAreas["PSNR"].BackColor = Color.DimGray;
                        this.chart1.Series["PSNR"].ChartType = SeriesChartType.Line;
                        this.chart1.Series["PSNR"].Color = Color.Aqua;
                        this.chart1.Series["PSNR"].ChartArea = "PSNR";
                        //開始解壓縮
                        Thread decodingTask = new Thread(new ThreadStart(interDecompressionThread));
                        decodingTask.Start();
                    }
                    else if (is_interfdc)
                    {
                        dialog.Title = "選擇原始檔案(比較PSNR)";
                        dialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.pcx, *.hpcx, *.tiff) | *.jpg; *.jpeg; *.png; *.pcx; *.hpcx; *.tiff";
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            if ((stream = dialog.OpenFile()) != null)
                            {
                                foreach (string filename in dialog.FileNames)
                                {
                                    if (filename.Contains(".hpcx"))
                                    {
                                        Huffman huffman = new Huffman();
                                        array = System.IO.File.ReadAllBytes(filename);
                                        Bitmap image = huffman.Decoding(array);
                                        frames.Add(image);
                                    }
                                    else
                                    {
                                        Bitmap image = (Bitmap)Image.FromFile(filename);
                                        frames.Add(image);
                                    }
                                }
                                stream.Close();
                            }
                        }
                        //設定圖表
                        this.chart1.BackColor = Color.LightGray;
                        this.chart1.Series.Add("PSNR");
                        this.chart1.ChartAreas.Add("PSNR");
                        this.chart1.ChartAreas["PSNR"].AxisY.Minimum = 0;
                        this.chart1.ChartAreas["PSNR"].AxisY.Maximum = 1600;
                        this.chart1.ChartAreas["PSNR"].AxisX.Interval = 2;
                        this.chart1.ChartAreas["PSNR"].AxisX.MajorGrid.LineColor = Color.Silver;
                        this.chart1.ChartAreas["PSNR"].AxisY.MajorGrid.LineColor = Color.Silver;
                        this.chart1.ChartAreas["PSNR"].BackColor = Color.DimGray;
                        this.chart1.Series["PSNR"].ChartType = SeriesChartType.Line;
                        this.chart1.Series["PSNR"].Color = Color.Aqua;
                        this.chart1.Series["PSNR"].ChartArea = "PSNR";
                        //開始解壓縮
                        Thread decodingTask = new Thread(new ThreadStart(diffDecompressionThread));
                        decodingTask.Start();
                    }
                    else
                    {
                        this.pictureBox1.Image = frames[prev_i];
                        this.pictureBox2.Image = frames[curr_i];
                        this.label1.Text = "PREVIOUS: 0/" + frames.Count;
                        this.label2.Text = "CURRENT: 1/" + frames.Count; 
                    }
                }
            }
        }
        //停止
        private void button3_Click(object sender, EventArgs e)
        {
            this.timer1.Enabled = false;
            this.timer2.Enabled = false;
            motion_vector_table.Clear(Color.White);
            prev_i = -1;
            curr_i = 0;
            if (is_mvc)
            {
                this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                this.label2.Text = "CURRENT: 1/" + decoding_frames.Count;
                this.BBMC.draw_chart(this.chart1, psnr, 1);
                this.pictureBox2.Image = decoding_frames[curr_i];
            }
            else if (is_ifc)
            {
                this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                this.label2.Text = "CURRENT: 1/" + decoding_frames.Count;
                this.IFC.draw_chart(this.chart1, psnr, 1);
                this.pictureBox2.Image = decoding_frames[curr_i];
            }
            else if (is_interfc || is_interfdc)
            {
                this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                this.label2.Text = "CURRENT: 1/" + decoding_frames.Count;
                this.ISsC.draw_chart(this.chart1, psnr, 1);
                this.pictureBox2.Image = decoding_frames[curr_i];
            }
            else
            {
                this.label1.Text = "PREVIOUS: 0/" + frames.Count;
                this.label2.Text = "CURRENT: 1/" + frames.Count;
                this.pictureBox2.Image = frames[curr_i];
            }
            this.pictureBox1.Image = null;
        }
        //播放
        private void button1_Click(object sender, EventArgs e)
        {
            this.timer2.Enabled = false;
            this.timer1.Interval = 100;
            this.timer1.Enabled = true;
        }
        //播放timer
        private void timer1_Tick(object sender, EventArgs e)
        {
            prev_i++;
            curr_i++;

            if (prev_i >= frames.Count - 1)
            {
                prev_i = frames.Count - 2;
            }
            if (is_mvc)
            {
                //第一幀無motion vector所以BMVs會比幀數少一張
                motion_vector_table.Clear(Color.White);
                this.pictureBox1.Image = decoding_frames[prev_i];
                for (int i = 0; i < BMVs[prev_i].motion_vectors.Count; i++)
                {
                    BBMC.draw_motion_vector_table(BMVs[prev_i].motion_vectors[i], motion_vector_table, 256, 256);
                }
                this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + decoding_frames.Count;
            }
            else if (is_ifc || is_interfc || is_interfdc)
            {
                this.pictureBox1.Image = decoding_frames[prev_i];
                this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + frames.Count;
            }
            else
            {
                this.pictureBox1.Image = frames[prev_i];
                this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + frames.Count;
            }

            if (curr_i >= frames.Count)
            {
                curr_i = frames.Count - 1;
                timer1.Enabled = false;
            }
            if (is_mvc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.BBMC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else if (is_ifc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.IFC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else if (is_interfc || is_interfdc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.ISsC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else
            {
                this.pictureBox2.Image = frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + frames.Count;
            }
        }
        //倒轉timer
        private void timer2_Tick(object sender, EventArgs e)
        {
            prev_i--;
            curr_i--;

            if (prev_i < 0)
            {
                prev_i = -1;
                if (is_mvc)
                {
                    this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                    motion_vector_table.Clear(Color.White);
                    this.BBMC.draw_chart(this.chart1, psnr, 1);
                }
                else if (is_ifc)
                {
                    this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                    this.IFC.draw_chart(this.chart1, psnr, 1);
                }
                else if (is_interfc || is_interfdc)
                {
                    this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                    this.ISsC.draw_chart(this.chart1, psnr, 1);
                }
                else
                {
                    this.label1.Text = "PREVIOUS: 0/" + frames.Count;
                }
                this.pictureBox1.Image = null;
            }
            else
            {
                if (is_mvc)
                {
                    //第一幀無motion vector所以BMVs會比幀數少一張
                    motion_vector_table.Clear(Color.White);
                    this.pictureBox1.Image = decoding_frames[prev_i];
                    for (int i = 0; i < BMVs[prev_i].motion_vectors.Count; i++)
                    {
                        BBMC.draw_motion_vector_table(BMVs[prev_i].motion_vectors[i], motion_vector_table, 256, 256);
                    }
                    this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + decoding_frames.Count;
                }
                else if (is_ifc || is_interfc || is_interfdc)
                {
                    this.pictureBox1.Image = decoding_frames[prev_i];
                    this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + frames.Count;
                }
                else
                {
                    this.pictureBox1.Image = frames[prev_i];
                    this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + frames.Count;
                }
            }


            if (curr_i < 0)
            {
                curr_i = 0;
            }
            if (is_mvc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.BBMC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else if (is_ifc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.IFC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else if (is_interfc || is_interfdc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.ISsC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else
            {
                this.pictureBox2.Image = frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + frames.Count;
            }
        }
        //後退
        private void button5_Click(object sender, EventArgs e)
        {
            this.timer1.Enabled = false;
            this.timer2.Enabled = false;
            prev_i--;
            curr_i--;

            if(prev_i < 0)
            {
                prev_i = -1;
                if (is_mvc)
                {
                    this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                    motion_vector_table.Clear(Color.White);
                    this.BBMC.draw_chart(this.chart1, psnr, 1);
                }
                else if (is_ifc)
                {
                    this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                    this.IFC.draw_chart(this.chart1, psnr, 1);
                }
                else if (is_interfc || is_interfdc)
                {
                    this.label1.Text = "PREVIOUS: 0/" + decoding_frames.Count;
                    this.ISsC.draw_chart(this.chart1, psnr, 1);
                }
                else
                {
                    this.label1.Text = "PREVIOUS: 0/" + frames.Count;
                }
                this.pictureBox1.Image = null;
            }
            else
            {
                if (is_mvc)
                {
                    //第一幀無motion vector所以BMVs會比幀數少一張
                    motion_vector_table.Clear(Color.White);
                    this.pictureBox1.Image = decoding_frames[prev_i];
                    for(int i=0; i<BMVs[prev_i].motion_vectors.Count; i++)
                    {
                        BBMC.draw_motion_vector_table(BMVs[prev_i].motion_vectors[i], motion_vector_table, 256, 256);
                    }
                    this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + decoding_frames.Count;
                }
                else if (is_ifc || is_interfc || is_interfdc)
                {
                    this.pictureBox1.Image = decoding_frames[prev_i];
                    this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + frames.Count;
                }
                else
                {
                    this.pictureBox1.Image = frames[prev_i];
                    this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + frames.Count;
                }
            }
            

            if (curr_i < 0)
            {
                curr_i = 0;
            }
            if (is_mvc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.BBMC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else if (is_ifc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.IFC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else if (is_interfc || is_interfdc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.ISsC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else
            {
                this.pictureBox2.Image = frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + frames.Count;
            }
        }
        //前進
        private void button4_Click(object sender, EventArgs e)
        {
            this.timer1.Enabled = false;
            this.timer2.Enabled = false;
            prev_i++;
            curr_i++;

            if(prev_i >= frames.Count - 1)
            {
                prev_i = frames.Count - 2;
            }
            if (is_mvc)
            {
                //第一幀無motion vector所以BMVs會比幀數少一張
                motion_vector_table.Clear(Color.White);
                this.pictureBox1.Image = decoding_frames[prev_i];
                for (int i = 0; i < BMVs[prev_i].motion_vectors.Count; i++)
                {
                    BBMC.draw_motion_vector_table(BMVs[prev_i].motion_vectors[i], motion_vector_table, 256, 256);
                }
                this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + decoding_frames.Count;
            }
            else if (is_ifc || is_interfc || is_interfdc)
            {
                this.pictureBox1.Image = decoding_frames[prev_i];
                this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + frames.Count;
            }
            else
            {
                this.pictureBox1.Image = frames[prev_i];
                this.label1.Text = "PREVIOUS: " + (prev_i + 1) + "/" + frames.Count;
            }

            if (curr_i >= frames.Count)
            {
                curr_i = frames.Count - 1;
            }
            if (is_mvc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.BBMC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else if (is_ifc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.IFC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else if (is_interfc || is_interfdc)
            {
                this.pictureBox2.Image = decoding_frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + decoding_frames.Count;
                this.ISsC.draw_chart(this.chart1, psnr, curr_i + 1);
            }
            else
            {
                this.pictureBox2.Image = frames[curr_i];
                this.label2.Text = "CURRENT: " + (curr_i + 1) + "/" + frames.Count;
            }
        }
        //暫停
        private void button2_Click(object sender, EventArgs e)
        {
            this.timer1.Enabled = false;
            this.timer2.Enabled = false;
        }
        //快轉
        private void button6_Click(object sender, EventArgs e)
        {
            this.timer1.Enabled = false;
            this.timer2.Enabled = false;
            this.timer1.Interval = 30;
            this.timer1.Enabled = true;
        }
        //倒轉
        private void button7_Click(object sender, EventArgs e)
        {
            this.timer1.Enabled = false;
            this.timer2.Interval = 100;
            this.timer2.Enabled = true;
        }

        private void absoluteDifferrenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Video files (*.mvc) | *.mvc";
            saveFileDialog.Title = "輸出檔案位置";
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            this.label1.Text = "PREVIOUS";
            this.label2.Text = "CURRENT";
            this.label4.Visible = true;
            this.label3.Visible = true;
            this.button1.Enabled = false;
            this.button2.Enabled = false;
            this.button3.Enabled = false;
            this.button4.Enabled = false;
            this.button5.Enabled = false;
            this.button6.Enabled = false;
            this.button7.Enabled = false;
            //開始壓縮
            BBMC = new Block_Based_Motion_Compression(frames);
            BBMC.difference_flag = 0;
            BBMC.search_flag = 0;
            Thread encodingTask = new Thread(new ThreadStart(encodingThread));
            encodingTask.Start();
        }

        private void meanSquareDifferenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Video files (*.mvc) | *.mvc";
            saveFileDialog.Title = "輸出檔案位置";
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            this.label1.Text = "PREVIOUS";
            this.label2.Text = "CURRENT";
            this.label4.Visible = true;
            this.label3.Visible = true;
            this.button1.Enabled = false;
            this.button2.Enabled = false;
            this.button3.Enabled = false;
            this.button4.Enabled = false;
            this.button5.Enabled = false;
            this.button6.Enabled = false;
            this.button7.Enabled = false;
            //開始壓縮
            BBMC = new Block_Based_Motion_Compression(frames);
            BBMC.difference_flag = 1;
            BBMC.search_flag = 0;
            Thread encodingTask = new Thread(new ThreadStart(encodingThread));
            encodingTask.Start();
        }

        private void absoluteDifferenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Video files (*.mvc) | *.mvc";
            saveFileDialog.Title = "輸出檔案位置";
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            this.label1.Text = "PREVIOUS";
            this.label2.Text = "CURRENT";
            this.label4.Visible = true;
            this.label3.Visible = true;
            this.button1.Enabled = false;
            this.button2.Enabled = false;
            this.button3.Enabled = false;
            this.button4.Enabled = false;
            this.button5.Enabled = false;
            this.button6.Enabled = false;
            this.button7.Enabled = false;
            //開始壓縮
            BBMC = new Block_Based_Motion_Compression(frames);
            BBMC.difference_flag = 0;
            BBMC.search_flag = 1;
            Thread encodingTask = new Thread(new ThreadStart(encodingThread));
            encodingTask.Start();
        }

        private void meanSquareDifferenceToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Video files (*.mvc) | *.mvc";
            saveFileDialog.Title = "輸出檔案位置";
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            this.label1.Text = "PREVIOUS";
            this.label2.Text = "CURRENT";
            this.label4.Visible = true;
            this.label3.Visible = true;
            this.button1.Enabled = false;
            this.button2.Enabled = false;
            this.button3.Enabled = false;
            this.button4.Enabled = false;
            this.button5.Enabled = false;
            this.button6.Enabled = false;
            this.button7.Enabled = false;
            //開始壓縮
            BBMC = new Block_Based_Motion_Compression(frames);
            BBMC.difference_flag = 1;
            BBMC.search_flag = 1;
            Thread encodingTask = new Thread(new ThreadStart(encodingThread));
            encodingTask.Start();
        }

        private void saveFileInterframeCompressionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Video files (*.intrafcp) | *.intrafcp";
            saveFileDialog.Title = "輸出檔案位置";
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            this.label1.Text = "PREVIOUS";
            this.label2.Text = "CURRENT";
            this.button1.Enabled = false;
            this.button2.Enabled = false;
            this.button3.Enabled = false;
            this.button4.Enabled = false;
            this.button5.Enabled = false;
            this.button6.Enabled = false;
            this.button7.Enabled = false;
            //開始壓縮
            Thread IntraCompressionTask = new Thread(new ThreadStart(intraCompressionThread));
            IntraCompressionTask.Start();
        }

        private void encodingThread()
        {
            //初始化壓縮資料
            BBMC.target_pic = this.pictureBox3;
            BBMC.candidate_pic = this.pictureBox4;
            BBMC.candidate_block = this.pictureBox1;
            BBMC.target_block = this.pictureBox2;
            BBMC.encoded_first_frame = this.encoded_first_frame;
            //
            BBMC.Encoding();
            System.IO.File.WriteAllBytes(saveFileDialog.FileName, BBMC.getEncodedBytes());
        }

        private void decodingThread()
        {
            BBMC = new Block_Based_Motion_Compression(frames);
            BBMC.motion_vector_table = this.pictureBox5;
            BBMC.psnr_chart = this.chart1;
            BBMC.target_block = this.pictureBox2;
            BBMC.candidate_block = this.pictureBox1;
            //
            BBMC.Decoding(film_array);
            decoding_frames = BBMC.getFrames();
            BMVs = BBMC.getBMVs();
            psnr = BBMC.getPSNRs();
        }

        private void intraCompressionThread()
        {
            IFC = new IntraFrame_Compression(frames);
            IFC.Encoding();
            System.IO.File.WriteAllBytes(saveFileDialog.FileName, IFC.getEncodedBytes());
        }

        private void intraDecompressionThread()
        {
            IFC = new IntraFrame_Compression(frames);
            decoding_frames = IFC.Decoding(film_array);
            psnr = IFC.getPSNRs();
            IFC.set_target_block(this.pictureBox1, decoding_frames[0]);
            IFC.set_candidate_block(this.pictureBox2, decoding_frames[1]);
        }

        private void interCompressionThread()
        {
            //初始化壓縮資料
            ISsC = new Interframe_Sub_sampling_Compression(frames);
            ISsC.flag = 0;
            ISsC.target_pic = this.pictureBox3;
            ISsC.candidate_pic = this.pictureBox4;
            ISsC.candidate_block = this.pictureBox1;
            ISsC.target_block = this.pictureBox2;
            ISsC.encoded_first_frame = this.encoded_first_frame;
            //
            ISsC.Encoding();
            System.IO.File.WriteAllBytes(saveFileDialog.FileName, ISsC.getEncodedBytes());
        }

        private void interDecompressionThread()
        {
            ISsC = new Interframe_Sub_sampling_Compression(frames);
            ISsC.flag = 0;
            ISsC.psnr_chart = this.chart1;
            ISsC.target_block = this.pictureBox2;
            ISsC.candidate_block = this.pictureBox1;
            //
            ISsC.Decoding(film_array);
            decoding_frames = ISsC.getFrames();
            psnr = ISsC.getPSNRs();
        }

        private void differenceCodingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Video files (*.interfdcp) | *.interfdcp";
            saveFileDialog.Title = "輸出檔案位置";
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            this.label1.Text = "PREVIOUS";
            this.label2.Text = "CURRENT";
            this.label4.Visible = true;
            this.label3.Visible = true;
            //開始壓縮
            Thread interCompressionTask = new Thread(new ThreadStart(diffCompressionThread));
            interCompressionTask.Start();
        }

        private void blockbasedDifferenceCodingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Video files (*.interfcp) | *.interfcp";
            saveFileDialog.Title = "輸出檔案位置";
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            this.label1.Text = "PREVIOUS";
            this.label2.Text = "CURRENT";
            this.label4.Visible = true;
            this.label3.Visible = true;
            //開始壓縮
            Thread interCompressionTask = new Thread(new ThreadStart(interCompressionThread));
            interCompressionTask.Start();
        }

        private void diffCompressionThread()
        {
            //初始化壓縮資料
            ISsC = new Interframe_Sub_sampling_Compression(frames);
            ISsC.flag = 1;
            ISsC.encoded_first_frame = this.encoded_first_frame;
            //
            ISsC.Encoding();
            System.IO.File.WriteAllBytes(saveFileDialog.FileName, ISsC.getEncodedBytes());
        }

        private void diffDecompressionThread()
        {
            ISsC = new Interframe_Sub_sampling_Compression(frames);
            ISsC.flag = 1;
            ISsC.psnr_chart = this.chart1;
            ISsC.target_block = this.pictureBox2;
            ISsC.candidate_block = this.pictureBox1;
            //
            ISsC.Decoding(film_array);
            decoding_frames = ISsC.getFrames();
            psnr = ISsC.getPSNRs();
        }
    }
}
