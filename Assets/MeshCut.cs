using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MeshCut
{

    private static Plane _blade;
    private static Transform _victimTransform;
    private static Mesh _victimMesh;

    private static bool[] _sides = new bool[3];

    // 切割后的子物体数据。左代表切割剩下的本体，右代表被切割出去的部分
    #region 切割后的子物体数据。左代表切割剩下的本体，右代表被切割出去的部分
    private static List<int>[] _leftGatherSubIndices = new List<int>[] { new List<int>(), new List<int>() };
    private static List<int>[] _rightGatherSubIndices = new List<int>[] { new List<int>(), new List<int>() };

    private static List<Vector3>[] _leftGatherAddedPoints = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
    private static List<Vector2>[] _leftGatherAddedUvs = new List<Vector2>[] { new List<Vector2>(), new List<Vector2>() };
    private static List<Vector3>[] _leftGatherAddedNormals = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };

    private static List<Vector3>[] _rightGatherAddedPoints = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
    private static List<Vector2>[] _rightGatherAddedUvs = new List<Vector2>[] { new List<Vector2>(), new List<Vector2>() };
    private static List<Vector3>[] _rightGatherAddedNormals = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };

    #endregion

    // 切割中的零时数据
    #region 切割过程中产生的零时数据
    private static Vector3 _leftPoint1 = Vector3.zero;
    private static Vector3 _leftPoint2 = Vector3.zero;
    private static Vector3 _rightPoint1 = Vector3.zero;
    private static Vector3 _rightPoint2 = Vector3.zero;

    private static Vector2 _leftUv1 = Vector3.zero;
    private static Vector2 _leftUv2 = Vector3.zero;
    private static Vector2 _rightUv1 = Vector3.zero;
    private static Vector2 _rightUv2 = Vector3.zero;

    private static Vector3 _leftNormal1 = Vector3.zero;
    private static Vector3 _leftNormal2 = Vector3.zero;
    private static Vector3 _rightNormal1 = Vector3.zero;
    private static Vector3 _rightNormal2 = Vector3.zero;
    #endregion


    // final arrays
    private static List<int>[] _leftFinalSubIndices = new List<int>[] { new List<int>(), new List<int>() };

    private static List<Vector3> _leftFinalVertices = new List<Vector3>();
    private static List<Vector3> _leftFinalNormals = new List<Vector3>();
    private static List<Vector2> _leftFinalUvs = new List<Vector2>();

    private static List<int>[] _rightFinalSubIndices = new List<int>[] { new List<int>(), new List<int>() };

    private static List<Vector3> _rightFinalVertices = new List<Vector3>();
    private static List<Vector3> _rightFinalNormals = new List<Vector3>();
    private static List<Vector2> _rightFinalUvs = new List<Vector2>();

    // capping stuff
    private static List<Vector3> _createdVertexPoints = new List<Vector3>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="victim">被切割的物体</param>
    /// <param name="anchorPoint"> 刀片上的一个点</param>
    /// <param name="normalDirection">刀片的法线方向 </param>
    /// <param name="capMaterial">用于填洞的材质</param>
    /// <returns></returns>
    public static GameObject[] Cut(GameObject victim, Vector3 anchorPoint, Vector3 normalDirection, Material capMaterial)
    {

        _victimTransform = victim.transform;
        _blade = new Plane(victim.transform.InverseTransformDirection(-normalDirection), victim.transform.InverseTransformPoint(anchorPoint));//根据被切割物体坐标体系创建刀片平面

        _victimMesh = victim.GetComponent<MeshFilter>().mesh;
        _victimMesh.subMeshCount = 2;//切割之后，mesh会又两个子网格（洞和表面），子网格列表数设置为2，因为要切割为两部分，我们可以为不同的subMesh不同的材质球（但最初模型不存在子网格，那么这个算法不适用于本来就存在多子网格的物体）

        ResetGatheringValues();//清理数据


        int index1 = 0;//点的索引
        int index2 = 0;
        int index3 = 0;

        _sides = new bool[3];

        int sub = 0;
        int[] indices = _victimMesh.triangles;//三角形顶点缓存，构成三角形点的序号，可在mesh三角形数组进行索引
        int[] secondIndices = _victimMesh.GetIndices(1);//子网格mesh

        for (int i = 0; i < indices.Length; i += 3)//遍历mesh的三角形
        {
            //三角形的三个顶点序号
            index1 = indices[i];
            index2 = indices[i + 1];
            index3 = indices[i + 2];
            //判断三个是否是在切面的正面
            _sides[0] = _blade.GetSide(_victimMesh.vertices[index1]);
            _sides[1] = _blade.GetSide(_victimMesh.vertices[index2]);
            _sides[2] = _blade.GetSide(_victimMesh.vertices[index3]);


            sub = 0;
            for (int k = 0; k < secondIndices.Length; k++)//遍历子网格,确认当前三角形是不是子网格（切割的时候，切割的洞）
            {
                if (secondIndices[k] == index1)
                {
                    sub = 1;
                    break;
                }
            }


            if (_sides[0] == _sides[1] && _sides[0] == _sides[2])
            { // 三个点在切面的同一侧

                if (_sides[0])//切面的正面，左侧（但由于是基于物体的坐标系...）
                { // left side
                    _leftGatherSubIndices[sub].Add(index1);
                    _leftGatherSubIndices[sub].Add(index2);
                    _leftGatherSubIndices[sub].Add(index3);

                }
                else
                {

                    _rightGatherSubIndices[sub].Add(index1);
                    _rightGatherSubIndices[sub].Add(index2);
                    _rightGatherSubIndices[sub].Add(index3);

                }

            }
            else//三个顶点不同在侧面的一侧
            { // cut face
                ResetFaceCuttingTemps();
                CutThisFace(sub, index1, index2, index3);
            }
        }


        // 组织最后的mesh数据，并填洞
        #region 组织最后的mesh数据，并填洞
        ResetFinalArrays();
        SetFinalArraysWithOriginals();
        AddNewTrianglesToFinalArrays();
        MakeCaps();//填洞 
        #endregion

        //生成新的gameobject，洞submesh1，材质球2个
        #region 生成gameobject 
        Mesh leftHalfMesh = new Mesh();
        leftHalfMesh.name = "Split Mesh Left";
        leftHalfMesh.vertices = _leftFinalVertices.ToArray();

        leftHalfMesh.subMeshCount = 2;
        leftHalfMesh.SetIndices(_leftFinalSubIndices[0].ToArray(), MeshTopology.Triangles, 0);
        leftHalfMesh.SetIndices(_leftFinalSubIndices[1].ToArray(), MeshTopology.Triangles, 1);

        leftHalfMesh.normals = _leftFinalNormals.ToArray();
        leftHalfMesh.uv = _leftFinalUvs.ToArray();


        Mesh rightHalfMesh = new Mesh();
        rightHalfMesh.name = "Split Mesh Right";
        rightHalfMesh.vertices = _rightFinalVertices.ToArray();

        rightHalfMesh.subMeshCount = 2;
        rightHalfMesh.SetIndices(_rightFinalSubIndices[0].ToArray(), MeshTopology.Triangles, 0);
        rightHalfMesh.SetIndices(_rightFinalSubIndices[1].ToArray(), MeshTopology.Triangles, 1);

        rightHalfMesh.normals = _rightFinalNormals.ToArray();
        rightHalfMesh.uv = _rightFinalUvs.ToArray();

        victim.name = "leftSide";
        victim.GetComponent<MeshFilter>().mesh = leftHalfMesh;

        Material[] mats = new Material[] { victim.GetComponent<MeshRenderer>().material, capMaterial };

        GameObject leftSideObj = victim;

        GameObject rightSideObj = new GameObject("rightSide", typeof(MeshFilter), typeof(MeshRenderer));
        rightSideObj.transform.position = _victimTransform.position;
        rightSideObj.transform.rotation = _victimTransform.rotation;
        rightSideObj.GetComponent<MeshFilter>().mesh = rightHalfMesh;


        leftSideObj.GetComponent<MeshRenderer>().materials = mats;
        rightSideObj.GetComponent<MeshRenderer>().materials = mats; 
        #endregion


        return new GameObject[] { leftSideObj, rightSideObj };

    }

    static void ResetGatheringValues()
    {

        _leftGatherSubIndices[0].Clear();
        _leftGatherSubIndices[1].Clear();
        _leftGatherAddedPoints[0].Clear();
        _leftGatherAddedPoints[1].Clear();
        _leftGatherAddedUvs[0].Clear();
        _leftGatherAddedUvs[1].Clear();
        _leftGatherAddedNormals[0].Clear();
        _leftGatherAddedNormals[1].Clear();

        _rightGatherSubIndices[0].Clear();
        _rightGatherSubIndices[1].Clear();
        _rightGatherAddedPoints[0].Clear();
        _rightGatherAddedPoints[1].Clear();
        _rightGatherAddedUvs[0].Clear();
        _rightGatherAddedUvs[1].Clear();
        _rightGatherAddedNormals[0].Clear();
        _rightGatherAddedNormals[1].Clear();

        _createdVertexPoints.Clear();

    }

    static void ResetFaceCuttingTemps()
    {

        _leftPoint1 = Vector3.zero;
        _leftPoint2 = Vector3.zero;
        _rightPoint1 = Vector3.zero;
        _rightPoint2 = Vector3.zero;

        _leftUv1 = Vector3.zero;
        _leftUv2 = Vector3.zero;
        _rightUv1 = Vector3.zero;
        _rightUv2 = Vector3.zero;

        _leftNormal1 = Vector3.zero;
        _leftNormal2 = Vector3.zero;
        _rightNormal1 = Vector3.zero;
        _rightNormal2 = Vector3.zero;

    }

    /// <summary>
    /// 切割一个三角面
    /// </summary>
    /// <param name="submesh">所属的子网格序号</param>
    /// <param name="index1">三角面顶点1序号</param>
    /// <param name="index2">三角面顶点2序号</param>
    /// <param name="index3">三角面顶点3序号</param>
    //三个顶点被一个平面分割，一般情况是会有两个点在平面的一侧，另一个点在一侧，并产生两个新的顶点。
    //一个三角面被一个平面分割，一般会生成三个三角面
    static void CutThisFace(int submesh, int index1, int index2, int index3)
    {

        int p = index1;
        for (int side = 0; side < 3; side++)
        {

            switch (side)
            {
                case 0:
                    p = index1;
                    break;
                case 1:
                    p = index2;
                    break;
                case 2:
                    p = index3;
                    break;

            }
            //用4个顶点来装要被分在不同侧的三个顶点，其中两个同一侧，一个异侧
            //顶点
            //UV
            //normal
            if (_sides[side])//在正面
            {
                if (_leftPoint1 == Vector3.zero)
                {

                    _leftPoint1 = _victimMesh.vertices[p];
                    _leftPoint2 = _leftPoint1;
                    _leftUv1 = _victimMesh.uv[p];
                    _leftUv2 = _leftUv1;
                    _leftNormal1 = _victimMesh.normals[p];
                    _leftNormal2 = _leftNormal1;

                }
                else
                {

                    _leftPoint2 = _victimMesh.vertices[p];
                    _leftUv2 = _victimMesh.uv[p];
                    _leftNormal2 = _victimMesh.normals[p];

                }
            }
            else//在背面
            {
                if (_rightPoint1 == Vector3.zero)
                {

                    _rightPoint1 = _victimMesh.vertices[p];
                    _rightPoint2 = _rightPoint1;
                    _rightUv1 = _victimMesh.uv[p];
                    _rightUv2 = _rightUv1;
                    _rightNormal1 = _victimMesh.normals[p];
                    _rightNormal2 = _rightNormal1;

                }
                else
                {

                    _rightPoint2 = _victimMesh.vertices[p];
                    _rightUv2 = _victimMesh.uv[p];
                    _rightNormal2 = _victimMesh.normals[p];

                }
            }
        }


        //生成两个新的点
        //顶点
        //法线
        //uv
        #region 生成两个新的点
        float factory = 0.0f;
        float distance = 0;

        _blade.Raycast(new Ray(_leftPoint1, (_rightPoint1 - _leftPoint1).normalized), out distance);//点沿着射线到平面的距离

        factory = distance / (_rightPoint1 - _leftPoint1).magnitude;//距离比例
        Vector3 newVertex1 = Vector3.Lerp(_leftPoint1, _rightPoint1, factory);//通过插值生成新的顶点
        Vector2 newUv1 = Vector2.Lerp(_leftUv1, _rightUv1, factory);//新顶点对应的uv
        Vector3 newNormal1 = Vector3.Lerp(_leftNormal1, _rightNormal1, factory);//新顶点对应的法线

        _createdVertexPoints.Add(newVertex1);//新生成的点，就是切割形成的洞的边缘的点

        _blade.Raycast(new Ray(_leftPoint2, (_rightPoint2 - _leftPoint2).normalized), out distance);

        factory = distance / (_rightPoint2 - _leftPoint2).magnitude;
        Vector3 newVertex2 = Vector3.Lerp(_leftPoint2, _rightPoint2, factory);
        Vector2 newUv2 = Vector2.Lerp(_leftUv2, _rightUv2, factory);
        Vector3 newNormal2 = Vector3.Lerp(_leftNormal2, _rightNormal2, factory);

        _createdVertexPoints.Add(newVertex2); 
        #endregion

        //生成两个新的三角面，两半mesh都需要添加（会造成重复的三角面）
        #region 生成两个新的三角面
        // first triangle
        AddLeftTriangle(submesh, newNormal1, new Vector3[] { _leftPoint1, newVertex1, newVertex2 },
        new Vector2[] { _leftUv1, newUv1, newUv2 },
        new Vector3[] { _leftNormal1, newNormal1, newNormal2 });

        // second triangle
        AddLeftTriangle(submesh, newNormal2, new Vector3[] { _leftPoint1, _leftPoint2, newVertex2 },
        new Vector2[] { _leftUv1, _leftUv2, newUv2 },
        new Vector3[] { _leftNormal1, _leftNormal2, newNormal2 });

        // first triangle
        AddRightTriangle(submesh, newNormal1, new Vector3[] { _rightPoint1, newVertex1, newVertex2 },
        new Vector2[] { _rightUv1, newUv1, newUv2 },
        new Vector3[] { _rightNormal1, newNormal1, newNormal2 });

        // second triangle
        AddRightTriangle(submesh, newNormal2, new Vector3[] { _rightPoint1, _rightPoint2, newVertex2 },
        new Vector2[] { _rightUv1, _rightUv2, newUv2 },
        new Vector3[] { _rightNormal1, _rightNormal2, newNormal2 }); 
        #endregion

    }


    /// 添加一个三角面
    static void AddLeftTriangle(int submesh, Vector3 faceNormal, Vector3[] points, Vector2[] uvs, Vector3[] normals)
    {

        int p1 = 0;
        int p2 = 1;
        int p3 = 2;

        Vector3 calculatedNormal = Vector3.Cross((points[1] - points[0]).normalized, (points[2] - points[0]).normalized);

        if (Vector3.Dot(calculatedNormal, faceNormal) < 0)
        {

            p1 = 2;
            p2 = 1;
            p3 = 0;
        }

        _leftGatherAddedPoints[submesh].Add(points[p1]);
        _leftGatherAddedPoints[submesh].Add(points[p2]);
        _leftGatherAddedPoints[submesh].Add(points[p3]);

        _leftGatherAddedUvs[submesh].Add(uvs[p1]);
        _leftGatherAddedUvs[submesh].Add(uvs[p2]);
        _leftGatherAddedUvs[submesh].Add(uvs[p3]);

        _leftGatherAddedNormals[submesh].Add(normals[p1]);
        _leftGatherAddedNormals[submesh].Add(normals[p2]);
        _leftGatherAddedNormals[submesh].Add(normals[p3]);

    }

    /// 添加一个三角面
    static void AddRightTriangle(int submesh, Vector3 faceNormal, Vector3[] points, Vector2[] uvs, Vector3[] normals)
    {


        int p1 = 0;
        int p2 = 1;
        int p3 = 2;

        Vector3 calculatedNormal = Vector3.Cross((points[1] - points[0]).normalized, (points[2] - points[0]).normalized);

        if (Vector3.Dot(calculatedNormal, faceNormal) < 0)
        {

            p1 = 2;
            p2 = 1;
            p3 = 0;
        }


        _rightGatherAddedPoints[submesh].Add(points[p1]);
        _rightGatherAddedPoints[submesh].Add(points[p2]);
        _rightGatherAddedPoints[submesh].Add(points[p3]);

        _rightGatherAddedUvs[submesh].Add(uvs[p1]);
        _rightGatherAddedUvs[submesh].Add(uvs[p2]);
        _rightGatherAddedUvs[submesh].Add(uvs[p3]);

        _rightGatherAddedNormals[submesh].Add(normals[p1]);
        _rightGatherAddedNormals[submesh].Add(normals[p2]);
        _rightGatherAddedNormals[submesh].Add(normals[p3]);

    }


    static void ResetFinalArrays()
    {

        _leftFinalSubIndices[0].Clear();
        _leftFinalSubIndices[1].Clear();
        _leftFinalVertices.Clear();
        _leftFinalNormals.Clear();
        _leftFinalUvs.Clear();

        _rightFinalSubIndices[0].Clear();
        _rightFinalSubIndices[1].Clear();
        _rightFinalVertices.Clear();
        _rightFinalNormals.Clear();
        _rightFinalUvs.Clear();

    }

    static void SetFinalArraysWithOriginals()
    {

        int p = 0;

        for (int submesh = 0; submesh < 2; submesh++)
        {

            for (int i = 0; i < _leftGatherSubIndices[submesh].Count; i++)
            {

                p = _leftGatherSubIndices[submesh][i];

                _leftFinalVertices.Add(_victimMesh.vertices[p]);
                _leftFinalSubIndices[submesh].Add(_leftFinalVertices.Count - 1);
                _leftFinalNormals.Add(_victimMesh.normals[p]);
                _leftFinalUvs.Add(_victimMesh.uv[p]);

            }

            for (int i = 0; i < _rightGatherSubIndices[submesh].Count; i++)
            {

                p = _rightGatherSubIndices[submesh][i];

                _rightFinalVertices.Add(_victimMesh.vertices[p]);
                _rightFinalSubIndices[submesh].Add(_rightFinalVertices.Count - 1);
                _rightFinalNormals.Add(_victimMesh.normals[p]);
                _rightFinalUvs.Add(_victimMesh.uv[p]);

            }

        }

    }

    static void AddNewTrianglesToFinalArrays()
    {

        for (int submesh = 0; submesh < 2; submesh++)
        {

            int count = _leftFinalVertices.Count;
            // add the new ones
            for (int i = 0; i < _leftGatherAddedPoints[submesh].Count; i++)
            {

                _leftFinalVertices.Add(_leftGatherAddedPoints[submesh][i]);
                _leftFinalSubIndices[submesh].Add(i + count);
                _leftFinalUvs.Add(_leftGatherAddedUvs[submesh][i]);
                _leftFinalNormals.Add(_leftGatherAddedNormals[submesh][i]);

            }

            count = _rightFinalVertices.Count;

            for (int i = 0; i < _rightGatherAddedPoints[submesh].Count; i++)
            {

                _rightFinalVertices.Add(_rightGatherAddedPoints[submesh][i]);
                _rightFinalSubIndices[submesh].Add(i + count);
                _rightFinalUvs.Add(_rightGatherAddedUvs[submesh][i]);
                _rightFinalNormals.Add(_rightGatherAddedNormals[submesh][i]);

            }
        }

    }

    private static List<Vector3> _capVertTracker = new List<Vector3>();
    private static List<Vector3> _capVertpolygon = new List<Vector3>();

    ///填洞
    static void MakeCaps()
    {

        _capVertTracker.Clear();

        for (int i = 0; i < _createdVertexPoints.Count; i++)//遍历所有新生成的点，因为新生成的点构成洞的边缘
            if (!_capVertTracker.Contains(_createdVertexPoints[i]))
            {
                _capVertpolygon.Clear();
                _capVertpolygon.Add(_createdVertexPoints[i]);
                _capVertpolygon.Add(_createdVertexPoints[i + 1]);

                _capVertTracker.Add(_createdVertexPoints[i]);
                _capVertTracker.Add(_createdVertexPoints[i + 1]);


                bool isDone = false;
                while (!isDone)
                {
                    isDone = true;

                    for (int k = 0; k < _createdVertexPoints.Count; k += 2)
                    { // go through the pairs

                        if (_createdVertexPoints[k] == _capVertpolygon[_capVertpolygon.Count - 1] && !_capVertTracker.Contains(_createdVertexPoints[k + 1]))
                        { // if so add the other

                            isDone = false;
                            _capVertpolygon.Add(_createdVertexPoints[k + 1]);
                            _capVertTracker.Add(_createdVertexPoints[k + 1]);

                        }
                        else if (_createdVertexPoints[k + 1] == _capVertpolygon[_capVertpolygon.Count - 1] && !_capVertTracker.Contains(_createdVertexPoints[k]))
                        {// if so add the other

                            isDone = false;
                            _capVertpolygon.Add(_createdVertexPoints[k]);
                            _capVertTracker.Add(_createdVertexPoints[k]);
                        }
                    }
                }

                FillCap(_capVertpolygon);

            }

    }

    //具体填洞
    static void FillCap(List<Vector3> vertices)
    {

        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();

        Vector3 center = Vector3.zero;
        foreach (Vector3 point in vertices)
            center += point;

        center = center / vertices.Count;


        Vector3 upward = Vector3.zero;
        // 90 degree turn
        upward.x = _blade.normal.y;
        upward.y = -_blade.normal.x;
        upward.z = _blade.normal.z;
        Vector3 left = Vector3.Cross(_blade.normal, upward);

        Vector3 displacement = Vector3.zero;
        Vector3 relativePosition = Vector3.zero;

        for (int i = 0; i < vertices.Count; i++)
        {

            displacement = vertices[i] - center;
            relativePosition = Vector3.zero;
            relativePosition.x = 0.5f + Vector3.Dot(displacement, left);
            relativePosition.y = 0.5f + Vector3.Dot(displacement, upward);
            relativePosition.z = 0.5f + Vector3.Dot(displacement, _blade.normal);

            uvs.Add(new Vector2(relativePosition.x, relativePosition.y));
            normals.Add(_blade.normal);
        }


        vertices.Add(center);
        normals.Add(_blade.normal);
        uvs.Add(new Vector2(0.5f, 0.5f));

        Vector3 calculatedNormal = Vector3.zero;
        int otherIndex = 0;
        for (int i = 0; i < vertices.Count; i++)
        {

            otherIndex = (i + 1) % (vertices.Count - 1);

            calculatedNormal = Vector3.Cross((vertices[otherIndex] - vertices[i]).normalized,
                                              (vertices[vertices.Count - 1] - vertices[i]).normalized);

            if (Vector3.Dot(calculatedNormal, _blade.normal) < 0)
            {

                triangles.Add(vertices.Count - 1);
                triangles.Add(otherIndex);
                triangles.Add(i);
            }
            else
            {

                triangles.Add(i);
                triangles.Add(otherIndex);
                triangles.Add(vertices.Count - 1);
            }

        }

        int index = 0;
        for (int i = 0; i < triangles.Count; i++)
        {

            index = triangles[i];
            _rightFinalVertices.Add(vertices[index]);
            _rightFinalSubIndices[1].Add(_rightFinalVertices.Count - 1);
            _rightFinalNormals.Add(normals[index]);
            _rightFinalUvs.Add(uvs[index]);

        }

        for (int i = 0; i < normals.Count; i++)
        {
            normals[i] = -normals[i];
        }

        int temp1, temp2;
        for (int i = 0; i < triangles.Count; i += 3)
        {

            temp1 = triangles[i + 2];
            temp2 = triangles[i];

            triangles[i] = temp1;
            triangles[i + 2] = temp2;
        }

        for (int i = 0; i < triangles.Count; i++)
        {

            index = triangles[i];
            _leftFinalVertices.Add(vertices[index]);
            _leftFinalSubIndices[1].Add(_leftFinalVertices.Count - 1);
            _leftFinalNormals.Add(normals[index]);
            _leftFinalUvs.Add(uvs[index]);

        }

    }

}