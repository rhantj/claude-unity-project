using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

public class BlackHoleLensControllerTests
{
    [Test]
    public void PushGlobals_WritesBlackHoleWorldPositionAndSchwarzschildRadius()
    {
        var bhGo = new GameObject("TestBH");
        bhGo.transform.position = new Vector3(3f, 0f, 5f);
        var bh = bhGo.AddComponent<BlackHole>();
        bh.Configure(1f, 4000f, 0.5f, 2.5f); // eventHorizonRadius = 2.5

        var camGo = new GameObject("TestCam");
        var cam = camGo.AddComponent<Camera>();

        var ctrlGo = new GameObject("TestController");
        var ctrl = ctrlGo.AddComponent<BlackHoleLensController>();
        ctrl.Configure(bh, cam);

        ctrl.PushGlobals();

        Vector4 worldPos = Shader.GetGlobalVector("_BHWorldPos");
        Assert.AreEqual(3f, worldPos.x, 0.001f);
        Assert.AreEqual(5f, worldPos.z, 0.001f);
        Assert.AreEqual(2.5f, Shader.GetGlobalFloat("_BHRs"), 0.001f);

        Object.DestroyImmediate(ctrlGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(bhGo);
    }

    [Test]
    public void PushGlobals_WritesDiskInnerOuterAndStepCount()
    {
        var bhGo = new GameObject("TestBH2");
        var bh = bhGo.AddComponent<BlackHole>();

        var camGo = new GameObject("TestCam2");
        var cam = camGo.AddComponent<Camera>();

        var ctrlGo = new GameObject("TestController2");
        var ctrl = ctrlGo.AddComponent<BlackHoleLensController>();
        ctrl.Configure(bh, cam);

        ctrl.PushGlobals();

        Assert.That(Shader.GetGlobalFloat("_BHDiskOuter"),
            Is.GreaterThan(Shader.GetGlobalFloat("_BHDiskInner")));
        Assert.That(Shader.GetGlobalInt("_BHStepCount"), Is.GreaterThan(0));

        Object.DestroyImmediate(ctrlGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(bhGo);
    }

    [Test]
    public void Properties_AreReadWrite_AndPushGlobalsReflectsThem()
    {
        var bhGo = new GameObject("TestBH3");
        var bh = bhGo.AddComponent<BlackHole>();

        var camGo = new GameObject("TestCam3");
        var cam = camGo.AddComponent<Camera>();

        var ctrlGo = new GameObject("TestController3");
        var ctrl = ctrlGo.AddComponent<BlackHoleLensController>();
        ctrl.Configure(bh, cam);

        ctrl.DiskInnerRadius = 7f;
        ctrl.DiskOuterRadius = 25f;
        ctrl.BeamingStrength = 0.5f;
        ctrl.StepCount = 600;
        ctrl.DiskColorTint = new Color(0.9f, 0.4f, 0.2f, 1f);

        ctrl.PushGlobals();

        Assert.That(Shader.GetGlobalFloat("_BHDiskInner"), Is.EqualTo(7f).Within(0.001f));
        Assert.That(Shader.GetGlobalFloat("_BHDiskOuter"), Is.EqualTo(25f).Within(0.001f));
        Assert.That(Shader.GetGlobalFloat("_BHBeaming"), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(Shader.GetGlobalInt("_BHStepCount"), Is.EqualTo(600));
        Vector4 tint = Shader.GetGlobalColor("_BHDiskColorTint");
        Assert.That(tint.x, Is.EqualTo(0.9f).Within(0.001f));

        Object.DestroyImmediate(ctrlGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(bhGo);
    }
}
