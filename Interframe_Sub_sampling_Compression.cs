using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace MyVideoPlayer
{
    class Interframe_Sub_sampling_Compression
    {
        //影片每一幀
        private List<Bitmap> frames = new List<Bitmap>();
        private List<Bitmap> decoding_frames = new List<Bitmap>();
        private Huffman huffman = new Huffman();//壓縮 / 解壓縮均會用到
        //壓縮
        private List<byte> data = new List<byte>();
        private Bitmap candidate = new Bitmap(8, 8);
        private Bitmap target = new Bitmap(8, 8);
        private byte[] outputData;
        public byte[] encoded_first_frame;
        //解壓縮資料
        private byte[] first_frame;
        private Bitmap first_image;
        private double[] psnr;
        private int psnr_index = 0;
        public Chart psnr_chart;
        //元件
        private Pen pen = new Pen(Brushes.Red);
        private Pen thin_pen = new Pen(Brushes.Red);
        public PictureBox target_pic, candidate_pic, target_block, candidate_block;
        private int cur_x = 469, cur_y = 63;
        private int prev_x = 50, prev_y = 63;
        //委派: 解決跨執行緒控制UI問題
        delegate void set_target_pic_callback(PictureBox p, Bitmap image);
        delegate void set_candidate_pic_callback(PictureBox p, Bitmap image);
        delegate void set_target_block_callback(PictureBox p, Bitmap image);
        delegate void set_candidate_block_callback(PictureBox p, Bitmap image);
        delegate void draw_chart_callback(Chart chart, double[] psnr, int index);
        //選項: 0: block-based difference coding , 1: difference coding
        public int flag = 0;

        public Interframe_Sub_sampling_Compression(List<Bitmap> input) 
        {
            for (int i = 0; i < input.Count; i++)
            {
                frames.Add(new Bitmap(input[i]));
            }
        }

        public void Decoding(byte[] array)
        {
            //先用huffman解壓縮第一張
            int i, j, k = 0, x, y, m, n, index;
            int first_size = 60997;
            //int first_size = array[13] + array[14] * 256 + 1;
            //擷取第一幀bytes
            first_frame = new byte[first_size];
            for (i = 0; i < first_size; i++)
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
            int frame_number = 0;
            if (flag == 1)
            {
                frame_number = (array.Length - first_size) / (256 * 256) + 1;//每一幀的大小: 65536 bytes(記得算上第一幀用huffmanu壓縮的)
            }
            else
            {
                frame_number = 16;
            }
            //第一幀PSNR無限大
            psnr = new double[frame_number];
            psnr[psnr_index] = 1600;
            psnr_index++;
            //解壓縮
            int gray, w, h;
            Color c;
            byte tmp;
            if (flag == 0)
            {
                for (k = 1; k < frame_number; k++)
                {
                    set_target_block(target_block, new Bitmap(decoding_frames[k - 1]));
                    w = decoding_frames[k - 1].Width;
                    h = decoding_frames[k - 1].Height;
                    Bitmap frame = new Bitmap(256, 256);
                    for (j = 0; j < 256; j += 8)
                    {
                        for (i = 0; i < 256; i += 8)
                        {
                            //從前一張解壓像素
                            if (array[index] == 0)
                            {
                                for (n = j; n < j + 8; n++)
                                {
                                    for (m = i; m < i + 8; m++)
                                    {
                                        c = decoding_frames[k - 1].GetPixel(m % w, n % h);
                                        frame.SetPixel(m % w, n % h, c);
                                    }
                                }
                                index++;
                            }
                            else
                            {
                                index++;//若第一個byte是1則後面64個bytes是差值
                                for (n = j; n < j + 8; n++)
                                {
                                    for (m = i; m < i + 8; m++)
                                    {
                                        //從前一張解壓像素
                                        c = decoding_frames[k - 1].GetPixel(m % w, n % h);
                                        tmp = array[index];
                                        gray = tmp & 0x7F;
                                        //若最高位元為1則表示負號
                                        if ((tmp & 0x80) == 0x1)
                                        {
                                            gray = 0 - gray;
                                        }
                                        gray += c.R;
                                        if (gray > 255)
                                        {
                                            gray = 255;
                                        }
                                        else if (gray < 0)
                                        {
                                            gray = 0;
                                        }
                                        frame.SetPixel(m % w, n % h, Color.FromArgb(gray, gray, gray));
                                        index++;
                                    }
                                }
                            }
                        }
                    }
                    //計算跟原本之間的PSNR
                    psnr[psnr_index] = calculate_PSNR(frame, frames[k]);
                    psnr_index++;
                    draw_chart(psnr_chart, psnr, psnr_index);
                    //新增
                    decoding_frames.Add(new Bitmap(frame));
                    set_candidate_block(candidate_block, frame);
                }
            }
            else
            {
                for (k = 1; k < frame_number; k++)
                {
                    set_target_block(target_block, new Bitmap(decoding_frames[k - 1]));
                    w = decoding_frames[k - 1].Width;
                    h = decoding_frames[k - 1].Height;
                    Bitmap frame = new Bitmap(256, 256);
                    for (j = 0; j < 256; j ++)
                    {
                        for (i = 0; i < 256; i ++)
                        {
                            //從前一張解壓像素
                            c = decoding_frames[k - 1].GetPixel(i, j);
                            tmp = array[index];
                            gray = tmp & 0x7F;
                            //若最高位元為1則表示負號
                            if ((tmp & 0x80) == 0x1)
                            {
                                gray = 0 - gray;   
                            }
                            gray += c.R;
                            if(gray > 255)
                            {
                                gray = 255;
                            }
                            else if(gray < 0)
                            {
                                gray = 0;
                            }
                            frame.SetPixel(i, j, Color.FromArgb(gray, gray, gray));
                            index++;
                        }
                    }
                    //計算跟原本之間的PSNR
                    psnr[psnr_index] = calculate_PSNR(frame, frames[k]);
                    psnr_index++;
                    draw_chart(psnr_chart, psnr, psnr_index);
                    //新增
                    decoding_frames.Add(new Bitmap(frame));
                    set_candidate_block(candidate_block, frame);
                }
            }
        }

        public void Encoding()
        {
            int i, j, k, tmp, m, n;
            //編碼第一幀: 檔頭 + 圖片 + 調色盤
            if (encoded_first_frame == null)
            {
                huffman.Encoding(frames[0]);
                huffman.makeFile();
                data = huffman.getEncodedBytes();
            }
            else
            {
                for (i = 0; i < encoded_first_frame.Length; i++)
                {
                    data.Add(encoded_first_frame[i]);
                }
            }
            /**
             * Difference 編碼:
             * 每一幀用65536 byte去記
             * 
             * Block-based Difference編碼:
             * 以8*8為一個block, 如果小於閥值就給1個byte並設為0
             * 若否則就給1個byte並設為1, 算出這個block跟對應block間的差值平均
             * 並用Diference Coding的方式編碼這格block ( 共 65byte )
             * 所以第一幀以後每一幀的大小不確定
             */
            byte diff;
            Color c1, c2;
            int w = frames[0].Width, h = frames[0].Height;
            if (flag == 0)
            {
                for (i = 1; i < frames.Count; i++)
                {
                    for (j = 0; j < frames[i].Height; j += 8)
                    {
                        for (k = 0; k < frames[i].Width; k += 8)
                        {
                            //取得target block
                            get_block(frames[i], target, k, j);
                            //放大
                            set_target_pic(target_pic, amplification(target));
                            //位移
                            set_target_block(target_block, updateLocation(frames[i], cur_x + k, cur_y + j));
                            //跟前一幀比對差異
                            //取得candidate block
                            get_block(frames[i - 1], candidate, k, j);
                            //放大
                            set_candidate_pic(candidate_pic, amplification(candidate));
                            //位移
                            set_candidate_block(candidate_block, updateLocation(frames[i - 1], prev_x + k, prev_y + j));
                            //比較差異
                            tmp = get_absolute_difference(candidate, target);
                            if (tmp < 100)
                            {
                                data.Add(0);
                            }
                            else
                            {
                                data.Add(1);
                                for (n=j; n<j+8; n++)
                                {
                                    for(m=k; m<k+8; m++)
                                    {
                                        c1 = frames[i - 1].GetPixel(m % w, n % h);
                                        c2 = frames[i].GetPixel(m % w, n % h);
                                        tmp = c2.R - c1.R;
                                        //差值範圍-128 ~ 128之間
                                        if (Math.Abs(tmp) > 0x7F)
                                        {
                                            tmp = 0x7F;
                                        }
                                        //最高bit代表正負
                                        if (tmp >= 0)
                                        {
                                            diff = 0;
                                        }
                                        else
                                        {
                                            diff = 0x80;
                                        }
                                        tmp = Math.Abs(tmp);
                                        diff += (byte)tmp;
                                        data.Add(diff);
                                        diff = 0;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                for(i=1; i<frames.Count; i++)
                {
                    for(j=0; j<frames[i].Height; j++)
                    {
                        for(k=0; k<frames[i].Width; k++)
                        {
                            c1 = frames[i - 1].GetPixel(k, j);
                            c2 = frames[i].GetPixel(k, j);
                            tmp = c2.R - c1.R;
                            //差值範圍-128 ~ 128之間
                            if (Math.Abs(tmp) > 0x7F)
                            {
                                tmp = 0x7F;
                            }
                            //最高bit代表正負
                            if(tmp >= 0)
                            {
                                diff = 0;
                            }
                            else
                            {
                                diff = 0x80;
                            }
                            tmp = Math.Abs(tmp);
                            diff += (byte)tmp;
                            data.Add(diff);
                            diff = 0;
                        }
                    }
                }
            }
            
            //輸出
            outputData = new byte[data.Count];
            for (i = 0; i < data.Count; i++)
            {
                outputData[i] = data[i];
            }
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

        private void get_block(Bitmap b, Bitmap t, int x, int y)
        {
            Color c;
            int i, j;
            for (j = y; j < y + 8; j++)
            {
                for (i = x; i < x + 8; i++)
                {
                    c = b.GetPixel(i % b.Width, j % b.Height);
                    t.SetPixel(i - x, j - y, c);
                }
            }
        }

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
            for (i = 0; i < 8; i++)
            {
                for (j = 0; j < 8; j++)
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
                for (i = 0; i < index; i++)
                {
                    chart.Series["PSNR"].Points.Add((int)psnr[i]);
                }
            }
        }
    }
}
