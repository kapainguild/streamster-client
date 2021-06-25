using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DynamicStreamer.Helpers
{
    public class CubeParser
    {

        public static CubeData Read(byte[] data)
        {
            using var mem = new MemoryStream(data);
            using var reader = new StreamReader(mem);

            int line = 1;
            bool readData = false;

            var state = new CubeData(false, 0, new Vector3(0, 0, 0), new Vector3(1, 1, 1), null);

            try
            {
                while (true)
                {
                    var str = reader.ReadLine();
                    var p = str.Split(' ');

                    if (!readData)
                    {
                        if (string.IsNullOrWhiteSpace(str) || str.StartsWith("#") || str.ToLower().StartsWith("title"))
                        {
                            // nothing, this is comment of empty string
                        }
                        else if (p[0] == "LUT_1D_SIZE")
                        {
                            var size = Int32.Parse(p[1]);
                            state = state with { Is3D = false, Size = size, Data = new float[size * 4] };
                        }
                        else if (p[0] == "LUT_3D_SIZE")
                        {
                            var size = Int32.Parse(p[1]);
                            state = state with { Is3D = true, Size = size, Data = new float[size * size * size * 4] };
                        }
                        else if (p[0] == "DOMAIN_MIN")
                            state = state with { DomainMin = new Vector3(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3])) };
                        else if (p[0] == "DOMAIN_MAX")
                            state = state with { DomainMax = new Vector3(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3])) };
                        else if (state.Size > 0)
                            readData = true;
                        else
                            throw new InvalidOperationException("Header not found");
                    }

                    if (readData)
                    {
                        var r = float.Parse(p[0]);
                        var g = float.Parse(p[1]);
                        var b = float.Parse(p[2]);

                        int reads = state.Is3D ? state.Size * state.Size * state.Size : state.Size;
                        
                        int offset = 0;
                        for(int q = 0; q < reads; q++)
                        {
                            state.Data[offset++] = r;
                            state.Data[offset++] = g;
                            state.Data[offset++] = b;
                            state.Data[offset++] = 1.0f;

                            str = reader.ReadLine();
                            if (str == null)
                                break;
                            p = str.Split(' ');
                            r = float.Parse(p[0]);
                            g = float.Parse(p[1]);
                            b = float.Parse(p[2]);
                            line++;
                        }
                        break;
                    }
                    line++;
                }
            }
            catch (Exception e)
            {
                Core.LogError(e, $"Failed to open cube file in line '{line}'");
                return null;
            }
            return state;
        }

    }

    public record CubeData(bool Is3D, int Size, Vector3 DomainMin, Vector3 DomainMax, float[] Data);
}
