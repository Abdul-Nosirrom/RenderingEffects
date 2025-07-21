using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FS.Rendering.Utility
{
    public static class CommandBufferExtensions
    {
        private static Mesh k_screenMesh;

        public static Mesh ScreenMesh
        {
            get
            {
                if (k_screenMesh) return k_screenMesh;
                
                k_screenMesh = new Mesh { name = "ScreenPlaneMesh" };
                k_screenMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3( 3f, -1f, 0f),
                    new Vector3(-1f,  3f, 0f)
                });
                k_screenMesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0, false);
                k_screenMesh.UploadMeshData(false);
                return k_screenMesh;
            }
        }
        
        public static void Draw(this CommandBuffer cmd, RenderTargetIdentifier rt, Material material, int pass)
        {
            cmd.SetRenderTarget(rt);//, 0, CubemapFace.Unknown, -1);
            //cmd.SetViewport(new Rect(0, 0, Screen.width, Screen.height));;
            cmd.DrawMesh(ScreenMesh, Matrix4x4.identity, material, 0, pass);
        }
        
        public static void BlitToRT(this CommandBuffer cmd, RenderTargetIdentifier src, RenderTargetIdentifier rt, Material material, int pass)
        {
            cmd.SetRenderTarget(rt);//, 0, CubemapFace.Unknown, -1);
            cmd.Blit(src, rt, material, pass);
            //cmd.SetViewport(new Rect(0, 0, Screen.width, Screen.height));;
            //cmd.DrawMesh(ScreenMesh, Matrix4x4.identity, material, 0, pass);
        }
    }
}