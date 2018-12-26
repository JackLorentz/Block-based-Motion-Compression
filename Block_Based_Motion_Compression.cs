using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace MyVideoPlayer
{
    //位移補償 => 幀間壓縮
    class Block_Based_Motion_Compression
    {
        //元件
        private Pen pen = new Pen(Brushes.Red);
        private Pen thin_pen = new Pen(Brushes.Red);
        public PictureBox target_pic, candidate_pic, target_block, candidate_block, motion_vector_table;
        //private Graphics target_block_canvas, candidate_block_canvas;
        private int cur_x = 469, cur_y = 63;
        private int prev_x = 50, prev_y = 63;
        //委派: 解決跨執行緒控制UI問題
        delegate void set_target_pic_callback(PictureBox p, Bitmap image);
        delegate void set_candidate_pic_callback(PictureBox p, Bitmap image);
        delegate void set_target_block_callback(PictureBox p, Bitmap image);
        delegate void set_candidate_block_callback(PictureBox p, Bitmap image);
        delegate void draw_chart_callback(Chart chart, double[] psnr, int index);
        //影片每一幀
        private List<Bitmap> frames = new List<Bitmap>();
        private List<Bitmap> decoding_frames = new List<Bitmap>();
        private Huffman huffman = new Huffman();//壓縮 / 解壓縮均會用到
        //壓縮資料
        private List<byte> data = new List<byte>();
        private Bitmap candidate = new Bitmap(8, 8);
        private Bitmap target = new Bitmap(8, 8);
        private byte[] outputData;
        public byte[] encoded_first_frame;
        //解壓縮資料
        private byte[] first_frame;
        private Bitmap first_image;
        private List<Bitmap_Motion_Vector> BMVs = new List<Bitmap_Motion_Vector>();//所有frames的motion vector
        private double[] psnr;
        private int psnr_index = 0;
        public Chart psnr_chart;
        //選項
        //0: absolute / 1: mean square
        public int difference_flag = 0;
        //0: exhausted / 1: three step
        public int search_flag = 0;

        public Block_Based_Motion_Compression(List<Bitmap> input)
        {
            Bitmap t;
            for(int i=0; i<input.Count; i++)
            {
                t = new Bitmap(input[i]);
                frames.Add(t);
            }
            pen.Width = 2.0F;
            thin_pen.Width = 1.0F;
        }

        public void Decoding(byte[] array)
        {
            //先用huffman解壓縮第一張
            int i, j, k, x, y, m, n, index;
            int first_size = 60997;
            //int first_size = array[13] + array[14] * 256 + 1;
            //擷取第一幀bytes
            first_frame = new byte[first_size];
            for (i=0; i<first_size; i++)
            {
                first_frame[i] = array[i];
            }
            first_image = huffman.Decoding(first_frame);
            decoding_frames.Add(new Bitmap(first_image));
            //顯示
            set_candidate_block(candidate_block, first_image);
            //再用motion vector方式解壓縮
            index = first_size;
            //計算frame數
            int frame_number = (array.Length - first_size) / 2048 + 1;//每一幀的大小: 2048 bytes(記得算上第一幀用huffmanu壓縮的)
            //第一幀PSNR無限大
            psnr = new double[frame_number];
            psnr[psnr_index] = 4000;
            psnr_index++;
            //解壓縮
            int w, h;
            Graphics mvt = motion_vector_table.CreateGraphics();
            Motion_Vector mv;
            Bitmap_Motion_Vector bmv;
            Color c;
            for (k=1; k<frame_number; k++)
            {
                mvt.Clear(Color.White);
                set_target_block(target_block, new Bitmap(decoding_frames[k - 1]));
                w = decoding_frames[k - 1].Width;
                h = decoding_frames[k - 1].Height;
                Bitmap frame = new Bitmap(256, 256);
                bmv = new Bitmap_Motion_Vector();
                for (j=0; j<256; j+=8)
                {
                    for(i=0; i<256; i+=8)
                    {
                        //從前一張解壓像素
                        x = array[index];
                        y = array[index + 1];
                        for(n=j; n<j+8; n++)
                        {
                            for(m=i; m<i+8; m++)
                            {
                                c = decoding_frames[k - 1].GetPixel((x + m - i) % w, (y + n - j) % h);
                                frame.SetPixel(m % w, n % h, c);
                            }
                        }
                        //紀錄motion vector
                        mv = new Motion_Vector();
                        mv.start_x = i;
                        mv.start_y = j;
                        mv.end_x = x;
                        mv.end_y = y;
                        draw_motion_vector_table(mv, mvt, w, h);
                        bmv.motion_vectors.Add(mv);
                        index += 2;
                    }
                }
                //計算跟原本之間的PSNR
                psnr[psnr_index] = calculate_PSNR(frame, frames[k]);
                psnr_index++;
                draw_chart(psnr_chart, psnr, psnr_index);
                //新增
                BMVs.Add(bmv);
                decoding_frames.Add(new Bitmap(frame));
                set_candidate_block(candidate_block, frame);
            }
        }

        public void Encoding()
        {
            //編碼第一幀: 檔頭 + 圖片 + 調色盤
            if(encoded_first_frame == null)
            {
                huffman.Encoding(frames[0]);
                huffman.makeFile();
                data = huffman.getEncodedBytes();
            }
            else
            {
                for(int i=0; i<encoded_first_frame.Length; i++)
                {
                    data.Add(encoded_first_frame[i]);
                }
            }
            /**
            Motion Vector編碼:
            每個像素用兩個byte存(x, y)
            所以第一幀以後每一幀給 32*32*2 = 2048 bytes ( 約2KB )的空間(32是因為是以8*8為一個基本block)
             */
            int[] motion_vector = new int[2];
            for (int i = 1; i < frames.Count; i++)
            {
                for (int j = 0; j < frames[i].Height; j += 8)
                {
                    for (int k = 0; k < frames[i].Width; k += 8)
                    {
                        //取得target block
                        get_block(frames[i], target, k, j);
                        //放大
                        set_target_pic(target_pic, amplification(target));
                        //位移
                        set_target_block(target_block, updateLocation(frames[i], cur_x + k, cur_y + j));
                        //從前一幀搜尋matching block
                        if(search_flag == 0)
                        {
                            motion_vector = exhausted_search(frames[i - 1], target, k, j);
                        }
                        else
                        {
                            motion_vector = three_step_search(frames[i - 1], target, k, j);
                        }
                        data.Add((byte)motion_vector[0]);
                        data.Add((byte)motion_vector[1]);
                    }
                }
            }
            //輸出
            outputData = new byte[data.Count];
            for (int i=0; i<data.Count; i++)
            {
                outputData[i] = data[i];
            }
        }

        public List<Bitmap_Motion_Vector> getBMVs()
        {
            return this.BMVs;
        }

        public List<Bitmap> getFrames()
        {
            return this.decoding_frames;
        }

        public byte[] getEncodedBytes()
        {
            return outputData;
        }

        public double[] getPSNRs()
        {
            return this.psnr;
        }

        //b: 前一幀, t: target block, (x, y): target block位置
        private int[] three_step_search(Bitmap b, Bitmap t, int x, int y)
        {
            int[] motion_vector = new int[2];
            int min = int.MaxValue, tmp = 0;
            int i, j, w = b.Width, h = b.Height;
            int min_i = 0, min_j = 0;

            //先看相同位置
            motion_vector[0] = x;
            motion_vector[1] = y;
            //取得candidate block
            get_block(b, candidate, x, y);
            //放大
            set_candidate_pic(candidate_pic, amplification(candidate));
            //位移
            set_candidate_block(candidate_block, updateLocation(b, prev_x + x, prev_y + y));
            //比較差異
            if (difference_flag == 0)
            {
                tmp = get_absolute_difference(candidate, t);
            }
            else
            {
                tmp = (int)get_mean_square_difference(candidate, t);
            }
            //threshold: 觀察出來
            if (tmp < 100)
            {
                return motion_vector;
            }
            //若相同位置不一樣則重頭開始找
            else
            {
                //Step size = 3 block
                for (j = (x - 24 + w) % w; j <= (y + 24) % h; j += 24)
                {
                    for (i = (y - 24 + h) % h; i <= (x + 24) % w; i += 24)
                    {
                        //取得candidate block
                        get_block(b, candidate, i, j);
                        //放大
                        set_candidate_pic(candidate_pic, amplification(candidate));
                        //位移
                        set_candidate_block(candidate_block, updateLocation(b, prev_x + i, prev_y + j));
                        //比較差異
                        if (difference_flag == 0)
                        {
                            tmp = get_absolute_difference(candidate, t);
                        }
                        else
                        {
                            tmp = (int)get_mean_square_difference(candidate, t);
                        }
                        //找最小值
                        if (min > tmp)
                        {
                            motion_vector[0] = i;
                            motion_vector[1] = j;
                            min = tmp;
                            min_i = i;
                            min_j = j;
                        }
                    }
                }
                //Step size = 2 block
                x = min_i;
                y = min_j;
                for (j = (y - 16 + h) % h; j <= (y + 16) % h; j += 16)
                {
                    for (i = (x - 16 + w) % w; i <= (x + 16) % w; i += 16)
                    {
                        //取得candidate block
                        get_block(b, candidate, i, j);
                        //放大
                        set_candidate_pic(candidate_pic, amplification(candidate));
                        //位移
                        set_candidate_block(candidate_block, updateLocation(b, prev_x + i, prev_y + j));
                        //比較差異
                        if (difference_flag == 0)
                        {
                            tmp = get_absolute_difference(candidate, t);
                        }
                        else
                        {
                            tmp = (int)get_mean_square_difference(candidate, t);
                        }
                        //找最小值
                        if (min > tmp)
                        {
                            motion_vector[0] = i;
                            motion_vector[1] = j;
                            min = tmp;
                            min_i = i;
                            min_j = j;
                        }
                    }
                }
                //Step size = 1 block
                x = min_i;
                y = min_j;
                for (j = (y - 8 + h) % h; j <= (y + 8) % h; j += 8)
                {
                    for (i = (x - 8 + w) % w; i <= (x + 8) % w; i += 8)
                    {
                        //取得candidate block
                        get_block(b, candidate, i, j);
                        //放大
                        set_candidate_pic(candidate_pic, amplification(candidate));
                        //位移
                        set_candidate_block(candidate_block, updateLocation(b, prev_x + i, prev_y + j));
                        //比較差異
                        if (difference_flag == 0)
                        {
                            tmp = get_absolute_difference(candidate, t);
                        }
                        else
                        {
                            tmp = (int)get_mean_square_difference(candidate, t);
                        }
                        //找最小值
                        if (min > tmp)
                        {
                            motion_vector[0] = i;
                            motion_vector[1] = j;
                            min = tmp;
                        }
                    }
                }
            }
            return motion_vector;
        }

        private int[] exhausted_search(Bitmap b, Bitmap t, int x, int y)
        {
            int[] motion_vector = new int[2];
            int min = int.MaxValue, tmp = 0;
            int i, j;

            //先看相同位置
            motion_vector[0] = x;
            motion_vector[1] = y;
            //取得candidate block
            get_block(b, candidate, x, y);
            //放大
            set_candidate_pic(candidate_pic, amplification(candidate));
            //位移
            set_candidate_block(candidate_block, updateLocation(b, prev_x + x, prev_y + y));
            //比較差異
            if (difference_flag == 0)
            {
                tmp = get_absolute_difference(candidate, t);
            }
            else
            {
                tmp = (int)get_mean_square_difference(candidate, t);
            }
            //threshold: 觀察出來
            if (tmp < 100)
            {
                return motion_vector;
            }
            //若相同位置不一樣則重頭開始找
            else
            {
                for (j = 0; j < b.Height; j += 8)
                {
                    for (i = 0; i < b.Width; i += 8)
                    {
                        //取得candidate block
                        get_block(b, candidate, i, j);
                        //放大
                        set_candidate_pic(candidate_pic, amplification(candidate));
                        //位移
                        set_candidate_block(candidate_block, updateLocation(b, prev_x + i, prev_y + j));
                        //比較差異
                        if(difference_flag == 0)
                        {
                            tmp = get_absolute_difference(candidate, t);
                        }
                        else
                        {
                            tmp = (int)get_mean_square_difference(candidate, t);
                        }
                        //找最小值
                        if (min > tmp)
                        {
                            motion_vector[0] = i;
                            motion_vector[1] = j;
                            min = tmp;
                        }
                    }
                }
            }
            return motion_vector;
        }
        //內存法存取圖片像素值
        private int get_sum(Bitmap b)
        {
            int sum = 0;
            Rectangle rect = new Rectangle(0, 0, 8, 8);

            BitmapData bmdata = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            System.IntPtr b_Ptr = bmdata.Scan0;

            int bytes_num = bmdata.Stride * b.Height;
            byte[] values = new byte[bytes_num];

            System.Runtime.InteropServices.Marshal.Copy(b_Ptr, values, 0, bytes_num);

            int i, j, k;
            for (i = 0; i < 8; i++)
            {
                for (j = 0; j < 8; j++)
                {
                    k = 3 * j;
                    sum += values[i * bmdata.Stride + k + 2];
                }
            }
            //解鎖位圖
            b.UnlockBits(bmdata);

            return sum;
        }

        private int get_absolute_difference(Bitmap c, Bitmap t)
        {
            int sum_of_diff = 0;
            Rectangle rect = new Rectangle(0, 0, 8, 8);

            BitmapData target_bmdata = t.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData candidate_bmdata = c.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            System.IntPtr target_Ptr = target_bmdata.Scan0;
            System.IntPtr candidate_Ptr = candidate_bmdata.Scan0;

            int target_bytes = target_bmdata.Stride * t.Height;
            byte[] target_values = new byte[target_bytes];
            int candidate_bytes = candidate_bmdata.Stride * c.Height;
            byte[] candidate_values = new byte[candidate_bytes];

            System.Runtime.InteropServices.Marshal.Copy(target_Ptr, target_values, 0, target_bytes);
            System.Runtime.InteropServices.Marshal.Copy(candidate_Ptr, candidate_values, 0, candidate_bytes);

            int i, j, k;
            for(i=0; i<8; i++)
            {
                for(j=0; j<8; j++)
                {
                    k = 3 * j;
                    sum_of_diff += Math.Abs(target_values[i * target_bmdata.Stride + k + 2] - candidate_values[i * candidate_bmdata.Stride + k + 2]);
                }
            }
            //解鎖位圖
            t.UnlockBits(target_bmdata);
            c.UnlockBits(candidate_bmdata);

            return sum_of_diff;
        }

        private double get_mean_square_difference(Bitmap c, Bitmap t)
        {
            double sum_of_diff = 0;
            Rectangle rect = new Rectangle(0, 0, 8, 8);

            BitmapData target_bmdata = t.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData candidate_bmdata = c.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            System.IntPtr target_Ptr = target_bmdata.Scan0;
            System.IntPtr candidate_Ptr = candidate_bmdata.Scan0;

            int target_bytes = target_bmdata.Stride * t.Height;
            byte[] target_values = new byte[target_bytes];
            int candidate_bytes = candidate_bmdata.Stride * c.Height;
            byte[] candidate_values = new byte[candidate_bytes];

            System.Runtime.InteropServices.Marshal.Copy(target_Ptr, target_values, 0, target_bytes);
            System.Runtime.InteropServices.Marshal.Copy(candidate_Ptr, candidate_values, 0, candidate_bytes);

            int i, j, k;
            double c_v, t_v;
            for (i = 0; i < 8; i++)
            {
                for (j = 0; j < 8; j++)
                {
                    k = 3 * j;
                    t_v = target_values[i * target_bmdata.Stride + k + 2];
                    c_v = candidate_values[i * candidate_bmdata.Stride + k + 2];
                    sum_of_diff += Math.Sqrt(Math.Pow(t_v - c_v, 2));
                }
            }
            //解鎖位圖
            t.UnlockBits(target_bmdata);
            c.UnlockBits(candidate_bmdata);

            return sum_of_diff;
        }

        private Bitmap amplification(Bitmap b)
        {
            Bitmap amplified_image = new Bitmap(64, 64);
            using (Graphics g = Graphics.FromImage(amplified_image))
            {
                g.DrawImage(b, 0, 0, 64, 64);
            }
            return amplified_image;
        }

        private Bitmap updateLocation(Bitmap b, int x, int y)
        {
            Bitmap newImage = new Bitmap(b.Width, b.Height);
            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.DrawImageUnscaled(b, 0, 0);
                g.DrawRectangle(pen, x, y, 8, 8);
            }
            return newImage;
        }

        private void get_block(Bitmap b, Bitmap t, int x, int y)
        {
            Color c;
            int i, j;
            for (j=y; j<y+8; j++)
            {
                for(i=x; i<x+8; i++)
                {
                    c = b.GetPixel(i % b.Width, j % b.Height);
                    t.SetPixel(i - x, j - y, c);
                }
            }
        }

        public void draw_motion_vector_table(Motion_Vector mv, Graphics g, int w, int h)
        {
            if (mv.start_x == mv.end_x && mv.start_y == mv.end_y)
            {
                g.FillRectangle(new SolidBrush(Color.Red), new Rectangle(mv.start_x + 4, mv.start_y + 4, 2, 2));
                return;
            }

            if (mv.start_x + 4 < w && mv.start_y + 4 < h && mv.end_x + 4 < w && mv.end_y + 4 < h)
            {
                g.DrawLine(thin_pen, new Point(mv.start_x + 4, mv.start_y + 4), new Point(mv.end_x + 4, mv.end_y + 4));
            }
        }

        public double calculate_PSNR(Bitmap b, Bitmap orig)
        {
            double sum = 0.0;
            double sum_of_diff = 0.0;
            Color c1, c2;
            int i, j;
            for(j=0; j<b.Height; j++)
            {
                for(i=0; i<b.Width; i++)
                {
                    c1 = orig.GetPixel(i, j);
                    c2 = b.GetPixel(i, j);
                    sum += Math.Pow(255, 2);
                    sum_of_diff += Math.Pow(c1.R - c2.R, 2);
                }
            }
            return sum / sum_of_diff;
        }

        //委派用的
        public void set_target_pic(PictureBox p, Bitmap image)
        {
            //判斷這物件是否在同個執行緒上
            if (p.InvokeRequired)
            {
                //表示在不同執行緒上
                set_target_pic_callback c = new set_target_pic_callback(set_target_pic);
                p.Invoke(c, p, image);
            }
            else
            {
                p.Image = image;
            }
        }

        public void set_candidate_pic(PictureBox p, Bitmap image)
        {
            //判斷這物件是否在同個執行緒上
            if (p.InvokeRequired)
            {
                //表示在不同執行緒上
                set_candidate_pic_callback c = new set_candidate_pic_callback(set_candidate_pic);
                p.Invoke(c, p, image);
            }
            else
            {
                p.Image = image;
            }
        }

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

        public void draw_chart(Chart chart, double[] psnr, int index)
        {
            //判斷這物件是否在同個執行緒上
            if (chart.InvokeRequired)
            {
                //表示在不同執行緒上
                draw_chart_callback c = new draw_chart_callback(draw_chart);
                chart.Invoke(c, chart, psnr, index);
            }
            else
            { 
                chart.Series["PSNR"].Points.Clear();
                int i;
                for(i=0; i<index; i++)
                {
                    chart.Series["PSNR"].Points.Add((int)psnr[i]);
                }
            }
        }

        public class Motion_Vector
        {
            public int start_x, end_x;
            public int start_y, end_y;
        }

        public class Bitmap_Motion_Vector
        {
            public List<Motion_Vector> motion_vectors = new List<Motion_Vector>();//每個frame的所有motion vector
        }
    }
}
