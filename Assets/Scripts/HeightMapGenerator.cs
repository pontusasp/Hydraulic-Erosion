using System.IO;
using UnityEngine;

public class HeightMapGenerator : MonoBehaviour {
    public int seed;
    public bool randomizeSeed;

    public int numOctaves = 7;
    public float persistence = .5f;
    public float lacunarity = 2;
    public float initialScale = 2;

    public bool useComputeShader = true;
    public ComputeShader heightMapComputeShader;

    public float[] GenerateHeightMap (int mapSize) {
        if (useComputeShader) {
            return GenerateHeightMapGPU (mapSize);
        }
        return LoadHeightMap("Assets/heightmap240x240.png", mapSize);
        return GenerateHeightMapCPU (mapSize);
    }

    float[] GenerateHeightMapGPU (int mapSize) {
        var prng = new System.Random (seed);

        Vector2[] offsets = new Vector2[numOctaves];
        for (int i = 0; i < numOctaves; i++) {
            offsets[i] = new Vector2 (prng.Next (-10000, 10000), prng.Next (-10000, 10000));
        }
        ComputeBuffer offsetsBuffer = new ComputeBuffer (offsets.Length, sizeof (float) * 2);
        offsetsBuffer.SetData (offsets);
        heightMapComputeShader.SetBuffer (0, "offsets", offsetsBuffer);

        int floatToIntMultiplier = 1000;
        float[] map = new float[mapSize * mapSize];

        ComputeBuffer mapBuffer = new ComputeBuffer (map.Length, sizeof (int));
        mapBuffer.SetData (map);
        heightMapComputeShader.SetBuffer (0, "heightMap", mapBuffer);

        int[] minMaxHeight = { floatToIntMultiplier * numOctaves, 0 };
        ComputeBuffer minMaxBuffer = new ComputeBuffer (minMaxHeight.Length, sizeof (int));
        minMaxBuffer.SetData (minMaxHeight);
        heightMapComputeShader.SetBuffer (0, "minMax", minMaxBuffer);

        heightMapComputeShader.SetInt ("mapSize", mapSize);
        heightMapComputeShader.SetInt ("octaves", numOctaves);
        heightMapComputeShader.SetFloat ("lacunarity", lacunarity);
        heightMapComputeShader.SetFloat ("persistence", persistence);
        heightMapComputeShader.SetFloat ("scaleFactor", initialScale);
        heightMapComputeShader.SetInt ("floatToIntMultiplier", floatToIntMultiplier);

        heightMapComputeShader.Dispatch (0, map.Length, 1, 1);

        mapBuffer.GetData (map);
        minMaxBuffer.GetData (minMaxHeight);
        mapBuffer.Release ();
        minMaxBuffer.Release ();
        offsetsBuffer.Release ();

        float minValue = (float) minMaxHeight[0] / (float) floatToIntMultiplier;
        float maxValue = (float) minMaxHeight[1] / (float) floatToIntMultiplier;

        for (int i = 0; i < map.Length; i++) {
            map[i] = Mathf.InverseLerp (minValue, maxValue, map[i]);
        }

        return map;
    }

    float[] GenerateHeightMapCPU (int mapSize) {
        var map = new float[mapSize * mapSize];
        seed = (randomizeSeed) ? Random.Range (-10000, 10000) : seed;
        var prng = new System.Random (seed);

        Vector2[] offsets = new Vector2[numOctaves];
        for (int i = 0; i < numOctaves; i++) {
            offsets[i] = new Vector2 (prng.Next (-1000, 1000), prng.Next (-1000, 1000));
        }

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        for (int y = 0; y < mapSize; y++) {
            for (int x = 0; x < mapSize; x++) {
                float noiseValue = 0;
                float scale = initialScale;
                float weight = 1;
                for (int i = 0; i < numOctaves; i++) {
                    Vector2 p = offsets[i] + new Vector2 (x / (float) mapSize, y / (float) mapSize) * scale;
                    noiseValue += Mathf.PerlinNoise (p.x, p.y) * weight;
                    weight *= persistence;
                    scale *= lacunarity;
                }
                map[y * mapSize + x] = noiseValue;
                minValue = Mathf.Min (noiseValue, minValue);
                maxValue = Mathf.Max (noiseValue, maxValue);
            }
        }

        // Normalize
        if (maxValue != minValue) {
            for (int i = 0; i < map.Length; i++) {
                map[i] = (map[i] - minValue) / (maxValue - minValue);
            }
        }

        return map;
    }

    float[] GetHeightmap(Texture2D texture) {
        float[] heights = new float[texture.width * texture.height];
        for (int y = 0; y < texture.height; y++) {
            for (int x = 0; x < texture.width; x++) {
                // get the pixel color
                Color pixel = texture.GetPixel(x, y);
                // Normalize the color value (from 0-255) to (0-1)
                float height = pixel.r / 255f;
                heights[x + y * texture.width] = height * 10;
            }
        }
        return heights;
    }


    float[] LoadHeightMap(string filename, int mapSize) {
        Debug.Log("Heightmap recieved request to load a texture with size " + mapSize + "x" + mapSize);
        // Create a new Texture2D and load the PNG file into it
        Texture2D heightmap = new Texture2D(mapSize, mapSize);
        heightmap.LoadImage(File.ReadAllBytes(filename));

        float[] map = GetHeightmap(heightmap);

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;
        for (int y = 0; y < mapSize; y++) {
            for (int x = 0; x < mapSize; x++) {
                minValue = Mathf.Min(map[x + y * heightmap.width], minValue);
                maxValue = Mathf.Max(map[x + y * heightmap.width], maxValue);
            }
        }
        
        // Normalize
        if (maxValue != minValue) {
            for (int i = 0; i < map.Length; i++) {
                map[i] = (map[i] - minValue) / (maxValue - minValue);
            }
        }

        return map;
    }
}