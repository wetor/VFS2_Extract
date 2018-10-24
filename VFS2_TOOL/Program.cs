using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ZLibNet;

namespace VFS2_TOOL
{
    class Program
    {
        static void Main(string[] args)
        {

            vfs_format vfs = new vfs_format(@"Z:\PSV GAME\[mai]泰拉瑞亚JP1.00\PCSG00286\data.vfs", true, true);
            vfs.init();
            vfs.extract();
            vfs.close();
            Console.Read();

        }
        
    }

    public class vfs_format
    {
        struct folder_t
        {
            public int flag;
            public int file_flag_begin;
            public List<file_t> files;
            public string path;
        }
        struct file_t
        {
            public int flag;
            public int folder_flag;
            public int offset;
            public int size;
            public string name;
            public bool compress;

        }
        string header = "VFS2";
        string vfs_filename;
        int file_num = 0;
        int filename_pos = 0;
        int basic_offset = 0;
        bool unpack = true;
        bool debug = true;
        FileStream fs;
        BinaryReader br;
        BinaryWriter bw;
        folder_t[] folder;
        public vfs_format(string filename,bool _unpack=true ,bool _debug=true )
        {
            unpack = _unpack;
            debug = _debug;
            vfs_filename = filename;
            if(unpack)
                fs = new FileStream(filename, FileMode.Open);
            else
                fs = new FileStream(filename, FileMode.Create);
            br = new BinaryReader(fs);
            bw = new BinaryWriter(fs);
            if (unpack)
            {
                if (br.ReadInt32() != 0x32534656)
                {
                    Console.WriteLine("Format error");
                    return;
                }
            }

        }
        public void init()
        {
            if (unpack)
            {
                folder = new folder_t[br.ReadInt32()];
                if (debug)
                    Console.WriteLine("Folder number:{0}",folder.Length);
                for(int i = 0; i < folder.Length; i++)
                {
                    fs.Seek(4, SeekOrigin.Current);//rand num
                    int temp = br.ReadInt32();
                    if (i != temp)
                        Console.WriteLine("FolderFlag Error");
                    folder[temp].flag = temp;
                    fs.Seek(8, SeekOrigin.Current);//rand num X2
                    folder[i].file_flag_begin = br.ReadInt32();
                    folder[i].files = new List<file_t>();
                    if (debug)
                        Console.WriteLine("FolderFlag:{0}  FileFlag Begin:{1}", folder[i].flag, folder[i].file_flag_begin);

                }
                file_num= br.ReadInt32();
                
                if (debug)
                    Console.WriteLine("File number:{0}", file_num);
                int temp_pos = (int)fs.Position;
                int count = 0;
                fs.Seek(file_num*24, SeekOrigin.Current);
                filename_pos = br.ReadInt32()+4;//跳过文件数
                basic_offset = (int)fs.Position;
                for (int i = 0; i < file_num; i++)
                {
                    fs.Seek(temp_pos+i*24, SeekOrigin.Begin);
                    file_t t_file = new file_t();
                    fs.Seek(4, SeekOrigin.Current);//rand num
                    t_file.flag= br.ReadInt32();
                    int temp= br.ReadInt32();
                    if (temp == 2)
                        t_file.compress = true;
                    else if(temp==0)
                        t_file.compress = false;

                    t_file.folder_flag= br.ReadInt32();
                    t_file.offset = br.ReadInt32()+ basic_offset;
                    t_file.size = br.ReadInt32();
                    fs.Seek(filename_pos+count, SeekOrigin.Begin);
                    int str_len= br.ReadInt32();
                    count += 4;
                    for(int k = 0; k < str_len; k++)
                    {
                        t_file.name += (char)br.Read();
                        count++;
                    }
                    folder[t_file.folder_flag].files.Add(t_file);
                    if (debug)
                        Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", t_file.folder_flag,t_file.name, t_file.flag, t_file.offset, t_file.size);

                }
                fs.Seek(filename_pos+count+4, SeekOrigin.Begin);//跳过文件夹数
                for(int i = 0; i < folder.Length; i++)
                {
                    folder[i].path = "\\";
                    int str_len= br.ReadInt32();
                    if (str_len == 0)
                        continue;
                    for (int k = 0; k < str_len; k++)
                        folder[i].path += (char)br.Read();
                    folder[i].path += "\\";

                }



            }

        }

        public void extract()
        {
            if (!unpack) return;
            string dir = vfs_filename + "_unpacked";
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            
            for (int i=0; i < folder.Length; i++)
            {
                
                Directory.CreateDirectory(dir+folder[i].path);
                for(int k = 0; k < folder[i].files.Count; k++)
                {
                    FileStream f_fs = new FileStream(dir + folder[i].path + folder[i].files[k].name, FileMode.Create);
                    fs.Seek(folder[i].files[k].offset, SeekOrigin.Begin);
                    if (debug)
                        Console.WriteLine("{0}  {1}",folder[i].files[k].name, folder[i].files[k].offset);
                    byte[] bye = br.ReadBytes(folder[i].files[k].size);
                    if (folder[i].files[k].compress)
                        bye= file_decompress(bye);

                    //byte[] unbye=ZLibCompressor.DeCompress(bye);
                    f_fs.Write(bye,0, bye.Length);
                    f_fs.Close();

                }
            }
        }
        byte[] file_decompress(byte[] bye)
        {
            
            MemoryStream ms = new MemoryStream(bye);
            BinaryReader t_br=new BinaryReader(ms);
            byte[] unbye = new byte[t_br.ReadInt32()];
            int block_num = unbye.Length / 32768 +1;
            int[] size = new int[block_num];
            int[] offset = new int[block_num];
            int i;
            for (i=0; i < block_num; i++)
            {
                offset[i] = t_br.ReadInt32();
                if (i > 0)
                    size[i - 1] = offset[i] - offset[i - 1];
            }
            size[i-1] = (int)ms.Length - offset[i-1];
            int index = 0;
            for (i = 0; i < block_num; i++)
            {
                ms.Seek(offset[i], SeekOrigin.Begin);
                byte[] t_bye=ZLibCompressor.DeCompress(t_br.ReadBytes(size[i]));

                t_bye.CopyTo(unbye, index);
                index += t_bye.Length;

            }
            t_br.Close();
            ms.Close();
            return unbye;
        }
        public void close()
        {
            br.Close();
            bw.Close();
            fs.Close();
        }

    }
}
