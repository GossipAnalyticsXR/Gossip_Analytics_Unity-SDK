using System;

[Serializable]
public class EyeGazeImagePayload
{
    public string sessionId;
    public string sceneName;
    public string timestampUtc;
    public string source;

    public string hitObjectName;
    public string hitObjectTag;
    public float hitX;
    public float hitY;
    public float hitZ;

    public float camPosX;
    public float camPosY;
    public float camPosZ;

    public float camRotX;
    public float camRotY;
    public float camRotZ;
    public float camRotW;

    public string imageBase64;
}