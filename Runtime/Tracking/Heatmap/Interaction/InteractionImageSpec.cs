using System;
using UnityEngine;

[Serializable]
public class InteractionImageSpec
{
    public string SceneName;
    public string TimestampUtc;

    public InteractionInfo Interaction;
    public CameraPose Camera;
}

[Serializable]
public class InteractionInfo
{
    public string ObjectId;
    public string ObjectName;
    public string InteractionType;
}

[Serializable]
public class CameraPose
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Fov;
    public float Aspect;
}