using UnityEngine;
using System.Collections;

public class SliceMesh : MonoBehaviour
{


    public Transform cutplane;// plane used for creating cutting plane to this location
    private float initialDensity; // density value, used in start & after cloning
    private Vector3 p1; // cut plane vertex positions, used for debug.draw
    private Vector3 p2;
    private Vector3 p3;


    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // draw debug: show vertices which are used to make plane
        Debug.DrawLine(p1, p2, Color.red);
        Debug.DrawLine(p1, p3, Color.red);
        Debug.DrawLine(p2, p3, Color.red);

        // controls
        if (Input.GetKeyDown("space")) // slice
        {
            //SliceIt();
            Mesh cutplanemesh = cutplane.GetComponent<MeshFilter>().mesh;
            Vector3[] cutplanevertices = cutplanemesh.vertices;
            MeshCut.Cut(gameObject, cutplane.TransformPoint(cutplanevertices[40]), cutplane.up,
            gameObject.GetComponent<Renderer>().material);
        }
        if (Input.GetKey("s")) // move object
        {
            //cutplane.position.y -= 0.01;
        }
        if (Input.GetKey("w")) // move object
        {
            //cutplane.position.y += 0.01;
        }
        if (Input.GetKey("a")) // move object
        {
            //cutplane.position.x -= 0.01;
        }
        if (Input.GetKey("d")) // move object
        {
            //cutplane.position.x += 0.01;
        }
        if (Input.GetKey("q")) // move object
        {
            //cutplane.rotation.z -= 0.01;
        }
        if (Input.GetKey("e")) // move object
        {
            //cutplane.rotation.z += 0.01;
        }
    }

    // fake-slicer function 3
    void SliceIt()
    {
        // original mesh
        Mesh mesh = GetComponent<MeshFilter>().mesh;

        // check object size, can we still slice it, or its too thin?
        //	if (mesh.bounds.size.x<0.05) return; // not used yet

        // get original mesh vertices
        Vector3[] vertices = mesh.vertices;

        // ok, ready to slice it, make clone for slice object
        GameObject clone = Instantiate(gameObject, transform.position + new Vector3(0, 0.25f, 0), transform.rotation) as GameObject; // place clone bit higher..to avoid collision clash

        // get slice mesh
        Mesh meshSlice = clone.GetComponent<MeshFilter>().mesh;
        Vector3[] verticesSlice = meshSlice.vertices;

        // get cutterplane mesh and vertices
        Mesh cutplanemesh = cutplane.GetComponent<MeshFilter>().mesh;
        Vector3[] cutplanevertices = cutplanemesh.vertices;

        //	create infinity-plane using 3 vertices from visible cutterplane
        p1 = cutplane.TransformPoint(cutplanevertices[40]);
        p2 = cutplane.TransformPoint(cutplanevertices[20]);
        p3 = cutplane.TransformPoint(cutplanevertices[0]);
        var myplane = new Plane(p1, p2, p3);

        // loop thru vertexes (of original object, but slice clone has same amount)
        for (var i = 0; i < vertices.Length; i++)
        {
            // Transforms the position x, y, z from local space to world space
            Vector3 tmpverts = transform.TransformPoint(vertices[i]); // original object vertices

            // if vertex is on "top" side of our plane, cut it = move vertices down
            if (myplane.GetSide(tmpverts))
            {
                // update original object vertices: move them down at cut plane, so it looks like we have sliced the object
                vertices[i] = transform.InverseTransformPoint(new Vector3(tmpverts.x, tmpverts.y - (myplane.GetDistanceToPoint(tmpverts)), tmpverts.z));

                // update slice object vertices: move them to where original box vertices were, so our slice takes the place of moved vertices
                verticesSlice[i] = transform.InverseTransformPoint(new Vector3(tmpverts.x, tmpverts.y, tmpverts.z));

            }
            else
            { // we are backside of cutplane

                // update slice object vertices: move them to cutplane
                verticesSlice[i] = transform.InverseTransformPoint(new Vector3(tmpverts.x, tmpverts.y - (myplane.GetDistanceToPoint(tmpverts)), tmpverts.z));

            }
        }

        // some mesh stuff
        mesh.vertices = vertices;
        mesh.RecalculateBounds();

        // adjust collision box size & location
       // GetComponent<CapsuleCollider>().height = mesh.bounds.size.y * 0.8f; // reset size, bit smaller than real
        //GetComponent<CapsuleCollider>().center = mesh.bounds.center; // reset center

        // some mesh stuff
        meshSlice.vertices = verticesSlice;
        meshSlice.RecalculateBounds();

        // adjust collision box size & location: adding mesh collider to slice, didnt work without convex
        //clone.GetComponent<CapsuleCollider>().height = meshSlice.bounds.size.y * 0.8f; // reset size
        //clone.GetComponent<CapsuleCollider>().center = meshSlice.bounds.center; // reset center

        // update mass after sliced (for original object, and slice clone)
       // GetComponent<Rigidbody>().mass = (mesh.bounds.size.x * mesh.bounds.size.y * mesh.bounds.size.z) * initialDensity;
        //clone.GetComponent<Rigidbody>().mass = (meshSlice.bounds.size.x * meshSlice.bounds.size.y * meshSlice.bounds.size.z) * initialDensity;

        // destroy script object from clone..otherwise we get looped spawns.. (should add this whole script to camera..?)
        Destroy(clone.GetComponent<SliceMesh>());

    }


}
