using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace MyVideoPlayer
{
    //Sub-Sampling => 幀內壓縮
    class IntraFrame_Compression
    {
        //壓縮
        private List<byte> data = new List<byte>();
        private List<Bitmap> frames = new List<Bitmap>();
        private List<Bitmap> decoding_frames = new List<Bitmap>();
        private Huffman huffman = new Huffman();//壓縮 / 解壓縮均會用到
        //解壓縮
        private double[] psnrs;
        private int psnr_index;
        //委派: 解決跨執行緒控制UI問題
        delegate void set_target_block_callback(PictureBox p, Bitmap image);
        delegate void set_candidate_block_callback(PictureBox p, Bitmap image);

        public IntraFrame_Compression(List<Bitmap> input)
        {
            for(int i=0; i<input.Count; i++)
            {
                frames.Add(new Bitmap(input[i]));
            }
        }
        //共5個byte
        private void make_header()
        {
            //版本
            data.Add(14);
            //Width
            data.Add((byte)frames[0].Width);
            data.Add((byte)(frames[0].Width >> 8));
            //Height
            data.Add((byte)frames[0].Height);
            data.Add((byte)(frames[0].Height >> 8));
        }

        public void Encoding()
        {
            make_header();
            Color c;
            int i, j, k;
            //以2*2取樣
            for(k=0; k<frames.Count; k++)
            {
                for(j=0; j<frames[k].Height; j+=2)
                {
                    for(i=0; i<frames[k].Width; i+=2)
                    {
                        c = frames[k].GetPixel(i, j);
                        data.Add(c.R);
                    }
                }
            }
        }

        public List<Bitmap> Decoding(byte[] array)
        {
            List<Bitmap> decoding_frames = new List<Bitmap>();
            int w = array[1] + array[2] * 256;
            int h = array[3] + array[4] * 256;
            Bitmap frame = new Bitmap(w, h);
            int frame_size = w * h / 4;
            int frame_number = (array.Length - 5) / frame_size;
            psnrs = new double[frame_number];
            int i, j, k, index = 0;

            for(k=0; k<frame_number; k++)
            {
                for(j=0; j<h; j+=2)
                {
                    for(i=0; i<w; i+=2)
                    {
                        frame.SetPixel(i, j, Color.FromArgb(array[index], array[index], array[index]));
                        frame.SetPixel((i + 1) % w, j, Color.FromArgb(array[index], array[index], array[index]));
                        frame.SetPixel(i, (j + 1) % h, Color.FromArgb(array[index], array[index], array[index]));
                        frame.SetPixel((i + 1) % w, (j + 1) % h, Color.FromArgb(array[index], array[index], array[index]));
                        index++;
                    }
                }
                psnrs[psnr_index] = calculate_PSNR(frame, frames[k]);
                psnr_index++;
                decoding_frames.Add(new Bitmap(frame));
            }

            return decoding_frames;
        }

        public byte[] getEncodedBytes()
        {
            byte[] array = new byte[data.Count];
            for(int i=0; i<data.Count; i++)
            {
                array[i] = data[i];
            }

            return array;
        }

        public double calculate_PSNR(Bitmap b, Bitmap orig)
        {
            double sum = 0.0;
            double sum_of_diff = 0.0;
            Color c1, c2;
            int i, j;
            for (j = 0; j < b.Height; j++)
            {
                for (i = 0; i < b.Width; i++)
                {
                    c1 = orig.GetPixel(i, j);
                    c2 = b.GetPixel(i, j);
                    sum += Math.Pow(255, 2);
                    sum_of_diff += Math.Pow(c1.R - c2.R, 2);
                }
            }
            return sum / sum_of_diff;
        }

        public double[] getPSNRs()
        {
            return this.psnrs;
        }

        public void draw_chart(Chart chart, double[] psnr, int index)
        {
            chart.Series["PSNR"].Points.Clear();
            int i;
            for (i = 0; i < index; i++)
            {
                chart.Series["PSNR"].Points.Add((int)psnr[i]);
            }
        }

        //委派
        public void set_target_block(PictureBox p, Bitmap image)
        {
            //判斷這物件是否在同個執行緒上
            if (p.InvokeRequired)
            {
                //表示在不同執行緒上
                set_target_block_callback c = new set_target_block_callback(set_target_block);
                p.Invoke(c, p, image);
            }
            else
            {
                p.Image = image;
            }
        }

        public void set_candidate_block(PictureBox p, Bitmap image)
        {
            //判斷這物件是否在同個執行緒上
            if (p.InvokeRequired)
            {
                //表示在不同執行緒上
                set_candidate_block_callback c = new set_candidate_block_callback(set_candidate_block);
                p.Invoke(c, p, image);
            }
            else
            {
                p.Image = image;
            }
        }
    }
}
