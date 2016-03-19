using UnityEngine;
using System.Collections;

// 必要な名前空間を宣言
using UnityEngine.UI;
using Windows.Kinect;

public class KinectController : MonoBehaviour
{
    // Kinect全体
    private KinectSensor sensor;
    private MultiSourceFrameReader reader;
    private CoordinateMapper mapper;

    // BodyIndex関連
    private FrameDescription bodyIndexDescription; // BodyIndexフレームの情報
    private byte[] bodyIndexData; // BodyIndexのデータを保持するバッファ

    // Color関連
    private FrameDescription colorDescription; // Colorフレームの情報
    private byte[] colorData; // Colorのデータを保持するバッファ

    // Depth関連
    private FrameDescription depthDescription; // Depthフレームの情報
    private ushort[] depthData; // Depthのデータを保持するバッファ

    // 画像表示関連
    public RawImage rawImage;   // RawImageオブジェクト（publicにしてInspectorで関連付け）
    private Texture2D texture;  // 動的に生成するテクスチャ
    private byte[] textureData; // テクスチャに書き込む色データのバッファ

    // はじめに一度だけ実行　初期化処理
    void Start ()
    {
        // Kinectセンサーを取得
        sensor = KinectSensor.GetDefault();

        // 複数データを扱うためのリーダーを開く（BodyIndex, Color, Depth）
        reader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.BodyIndex
                                                    | FrameSourceTypes.Color
                                                    | FrameSourceTypes.Depth);

        // データ間の対応情報を取得するためのマッパーを用意
        mapper = sensor.CoordinateMapper;

        // BodyIndexのフレーム情報を取得し、データを受け取るバッファを生成
        bodyIndexDescription = sensor.BodyIndexFrameSource.FrameDescription;
        bodyIndexData = new byte[bodyIndexDescription.Width * bodyIndexDescription.Height];

        // Colorのフレーム情報を取得し、データを受け取るバッファを作成
        colorDescription = sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
        colorData = new byte[colorDescription.Width * colorDescription.Height * colorDescription.BytesPerPixel];

        // Depthのフレーム情報を取得し、データを受け取るバッファを作成
        depthDescription = sensor.DepthFrameSource.FrameDescription;
        depthData = new ushort[depthDescription.Width * depthDescription.Height];

        // BodyIndexの解像度に合わせてテクスチャ、データバッファを生成
        texture = new Texture2D(bodyIndexDescription.Width, bodyIndexDescription.Height, TextureFormat.RGBA32, false);
        textureData = new byte[texture.width * texture.height * 4];

        // 生成したテクスチャをRawImageにセット
        rawImage.texture = texture;

        // センサーをオープン
        if(!sensor.IsOpen)
        {
            sensor.Open();
        }
	}

    // オブジェクトが破棄されるときに実行　終了処理
    void OnDestroy()
    {
        // リーダーを終了
        if (reader != null)
        {
            reader.Dispose();
            reader = null;
        }

        // センサーを終了
        if (sensor != null)
        {
            sensor.Close();
            sensor = null;
        }
    }

    // 毎フレーム実行される　更新処理
    void Update ()
    {
	    if(reader != null)
        {
            // 最新フレームを取得
            var frame = reader.AcquireLatestFrame();
            if(frame != null)
            {
                // BodyIndexの最新フレームを取得
                using (var bodyIndexFrame = frame.BodyIndexFrameReference.AcquireFrame())
                {
                    if(bodyIndexFrame != null)
                    {
                        // BodyIndexデータをバッファにコピー
                        bodyIndexFrame.CopyFrameDataToArray(bodyIndexData);
                    }
                }

                // Colorの最新フレームを取得
                using (var colorFrame = frame.ColorFrameReference.AcquireFrame())
                {
                    if(colorFrame != null)
                    {
                        // Colorデータをバッファにコピー
                        colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Rgba);
                    }
                }

                // Depthの最新フレームを取得
                using (var depthFrame = frame.DepthFrameReference.AcquireFrame())
                {
                    if (depthFrame != null)
                    {
                        // Depthデータをバッファにコピー
                        depthFrame.CopyFrameDataToArray(depthData);
                    }
                }
            }
        }

        // データ間の対応情報を取得
        ColorSpacePoint[] colorSpacePoints = new ColorSpacePoint[bodyIndexData.Length];
        mapper.MapDepthFrameToColorSpace(depthData, colorSpacePoints);

        // 各画素ごとにテクスチャの色を決定する
        for (int i=0; i<bodyIndexData.Length; ++i)
        {
            byte r, g, b, a;

            // BodyInexデータの値が255以外であれば人物領域
            if(bodyIndexData[i] != 255)
            {
                // 対応するColorデータの座標を取得
                ColorSpacePoint point = colorSpacePoints[i];
                int colorX = (int)point.X;
                int colorY = (int)point.Y;
                int colorIndex = colorDescription.Width * colorY + colorX;

                // 参照可能な範囲内かチェック
                if(0 <= colorX && colorX < colorDescription.Width
                    && 0 <= colorY && colorY <colorDescription.Height)
                {
                    // Colorデータを参照して色を決定
                    r = colorData[colorIndex * 4 + 0];
                    g = colorData[colorIndex * 4 + 1];
                    b = colorData[colorIndex * 4 + 2];
                    a = 255;
                }
                else
                {
                    r = 0;
                    g = 0;
                    b = 0;
                    a = 255;
                }
            }
            else
            {
                // aを0にして透明に
                r = 0;
                g = 0;
                b = 0;
                a = 0;
            }

            // テクスチャデータを更新する
            textureData[i * 4 + 0] = r;
            textureData[i * 4 + 1] = g;
            textureData[i * 4 + 2] = b;
            textureData[i * 4 + 3] = a;
        }

        // テクスチャデータをロードして適用する
        texture.LoadRawTextureData(textureData);
        texture.Apply();
    }
}
