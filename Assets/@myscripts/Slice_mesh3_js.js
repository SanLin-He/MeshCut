// FakeSlicer 3 (mgear)

var cutplane: Transform; // plane used for creating cutting plane to this location
//var cloneprefab : Transform; // clone prefab, all the slices are cloned from this

private var initialDensity : float; // density value, used in start & after cloning
private var p1:Vector3; // cut plane vertex positions, used for debug.draw
private var p2:Vector3;
private var p3:Vector3;

function Start()
{
	// get density (used for calculating mass for slices)
		//initialDensity = GetComponent.<Rigidbody>().mass/(GetComponent(MeshFilter).mesh.bounds.size.x*GetComponent(MeshFilter).mesh.bounds.size.y*GetComponent(MeshFilter).mesh.bounds.size.z);
}

// fake-slicer function 3
function SliceIt() 
{
	// original mesh
	var mesh : Mesh = GetComponent(MeshFilter).mesh;

	// check object size, can we still slice it, or its too thin?
	//	if (mesh.bounds.size.x<0.05) return; // not used yet

	// get original mesh vertices
	var vertices : Vector3[] = mesh.vertices;

	// ok, ready to slice it, make clone for slice object
	var clone : Transform;
	clone = Instantiate(transform, transform.position+Vector3(0,0.25,0),transform.rotation); // place clone bit higher..to avoid collision clash

	// get slice mesh
	var meshSlice : Mesh = clone.GetComponent(MeshFilter).mesh;
	var verticesSlice : Vector3[] = meshSlice.vertices;

	// get cutterplane mesh and vertices
	var cutplanemesh : Mesh = cutplane.GetComponent(MeshFilter).mesh;
	var cutplanevertices : Vector3[] = cutplanemesh.vertices;
	
	//	create infinity-plane using 3 vertices from visible cutterplane
	p1 = cutplane.TransformPoint(cutplanevertices[40]);
	p2 = cutplane.TransformPoint(cutplanevertices[20]);
	p3 = cutplane.TransformPoint(cutplanevertices[0]);
	var myplane = Plane(p1,p2,p3);

	// loop thru vertexes (of original object, but slice clone has same amount)
	for (var i = 0; i < vertices.Length; i++)
	{
		// Transforms the position x, y, z from local space to world space
		tmpverts = transform.TransformPoint(vertices[i]); // original object vertices

		// if vertex is on "top" side of our plane, cut it = move vertices down
		if (myplane.GetSide(tmpverts))
		{
			// update original object vertices: move them down at cut plane, so it looks like we have sliced the object
			vertices[i] = transform.InverseTransformPoint(Vector3(tmpverts.x,tmpverts.y-(myplane.GetDistanceToPoint(tmpverts)),tmpverts.z));
			
			// update slice object vertices: move them to where original box vertices were, so our slice takes the place of moved vertices
			verticesSlice[i] = transform.InverseTransformPoint(Vector3(tmpverts.x,tmpverts.y,tmpverts.z));
		
		}else{ // we are backside of cutplane

			// update slice object vertices: move them to cutplane
			verticesSlice[i] = transform.InverseTransformPoint(Vector3(tmpverts.x,tmpverts.y-(myplane.GetDistanceToPoint(tmpverts)),tmpverts.z));

		}
	}

	// some mesh stuff
	mesh.vertices = vertices;
	mesh.RecalculateBounds();

	// adjust collision box size & location
	//GetComponent.<Collider>().height = mesh.bounds.size.y*0.8; // reset size, bit smaller than real
	//GetComponent.<Collider>().center = mesh.bounds.center; // reset center

	// some mesh stuff
	meshSlice.vertices = verticesSlice;
	meshSlice.RecalculateBounds();

	// adjust collision box size & location: adding mesh collider to slice, didnt work without convex
	//clone.GetComponent.<Collider>().height = meshSlice.bounds.size.y*0.8; // reset size
	//clone.GetComponent.<Collider>().center = meshSlice.bounds.center; // reset center

	// update mass after sliced (for original object, and slice clone)
	//GetComponent.<Rigidbody>().mass = (mesh.bounds.size.x*mesh.bounds.size.y*mesh.bounds.size.z)*initialDensity;
	//clone.GetComponent.<Rigidbody>().mass = (meshSlice.bounds.size.x*meshSlice.bounds.size.y*meshSlice.bounds.size.z)*initialDensity;

	// destroy script object from clone..otherwise we get looped spawns.. (should add this whole script to camera..?)
	Destroy(clone.GetComponent("Slice_mesh3_js"));

}

// main loop
function Update () 
{
	// draw debug: show vertices which are used to make plane
	Debug.DrawLine (p1,p2, Color.red);
	Debug.DrawLine (p1,p3, Color.red);
	Debug.DrawLine (p2,p3, Color.red);

	// controls
	if (Input.GetKeyDown("space")) // slice
	{
		//SliceIt();
	}
	if (Input.GetKey("s")) // move object
	{
		cutplane.position.y-=0.01;
	}
	if (Input.GetKey("w")) // move object
	{
		cutplane.position.y+=0.01;
	}
	if (Input.GetKey("a")) // move object
	{
		cutplane.position.x-=0.01;
	}
	if (Input.GetKey("d")) // move object
	{
		cutplane.position.x+=0.01;
	}
	if (Input.GetKey("q")) // move object
	{
		cutplane.rotation.z-=0.01;
	}
	if (Input.GetKey("e")) // move object
	{
		cutplane.rotation.z+=0.01;
	}
	
}
