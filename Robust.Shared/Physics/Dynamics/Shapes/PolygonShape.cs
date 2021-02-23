/*
Microsoft Permissive License (Ms-PL)

This license governs use of the accompanying software. If you use the software, you accept this license.
If you do not accept the license, do not use the software.

1. Definitions
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under
U.S. copyright law.
A "contribution" is the original software, or any additions or changes to the software.
A "contributor" is any person that distributes its contribution under this license.
"Licensed patents" are a contributor's patent claims that read directly on its contribution.

2. Grant of Rights
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3,
each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution,
prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3,
each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to
make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or
derivative works of the contribution in the software.

3. Conditions and Limitations
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software,
your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark,
and attribution notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by
including a complete copy of this license with your distribution.
If you distribute any portion of the software in compiled or object code form, you may only do so under a license that
complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees or conditions.
You may have additional consumer rights under your local laws which this license cannot change.
To the extent permitted under your local laws, the contributors exclude the implied warranties of
merchantability, fitness for a particular purpose and non-infringement.
*/

using System;
using System.Collections.Generic;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.Physics.Dynamics.Shapes
{
    [Serializable, NetSerializable]
    public class PolygonShape : IPhysShape
    {
        /// <summary>
        ///     Counter-clockwise (CCW) order.
        /// </summary>
        [ViewVariables]
        public List<Vector2> Vertices
        {
            get => _vertices;
            set
            {
                _vertices = value;

                var configManager = IoCManager.Resolve<IConfigurationManager>();
                DebugTools.Assert(_vertices.Count >= 3 && _vertices.Count <= configManager.GetCVar(CVars.MaxPolygonVertices));


                if (configManager.GetCVar(CVars.ConvexHullPolygons))
                {
                    //FPE note: This check is required as the GiftWrap algorithm early exits on triangles
                    //So instead of giftwrapping a triangle, we just force it to be clock wise.
                    if (_vertices.Count <= 3)
                        _vertices.ForceCounterClockwise();
                    else
                        _vertices = GiftWrap.GetConvexHull(_vertices);
                }

                _normals = new List<Vector2>(_vertices.Count);

                // Compute normals. Ensure the edges have non-zero length.
                for (int i = 0; i < _vertices.Count; ++i)
                {
                    int next = i + 1 < _vertices.Count ? i + 1 : 0;
                    Vector2 edge = _vertices[next] - _vertices[i];
                    DebugTools.Assert(edge.LengthSquared > float.Epsilon * float.Epsilon);

                    //FPE optimization: Normals.Add(MathHelper.Cross(edge, 1.0f));
                    Vector2 temp = new Vector2(edge.Y, -edge.X);
                    _normals.Add(temp.Normalized);
                }

                // Compute the polygon mass data
                // ComputeProperties();
            }
        }

        private List<Vector2> _vertices = new();

        [ViewVariables]
        public List<Vector2> Normals => _normals;

        private List<Vector2> _normals = new();

        public int ChildCount => 1;

        /// <summary>
        /// The radius of this polygon.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float Radius
        {
            get => _radius;
            set
            {
                if (MathHelper.CloseTo(_radius, value)) return;
                _radius = value;
            }
        }

        private float _radius;

        public ShapeType ShapeType => ShapeType.Polygon;

        public PolygonShape()
        {
            _radius = IoCManager.Resolve<IConfigurationManager>().GetCVar(CVars.PolygonRadius);
        }

        public void SetAsBox(float width, float height)
        {
            Vertices = new List<Vector2>()
            {
                new(-width, -height),
                new(width, -height),
                new(width, height),
                new(-width, height),
            };
        }

        public void ExposeData(ObjectSerializer serializer)
        {
            serializer.DataField(this, x => x.Vertices, "vertices", new List<Vector2>());
            // ComputeProperties();
        }

        /// <summary>
        ///     A temporary optimisation that bypasses GiftWrapping until we get proper AABB and Rect collisions
        /// </summary>
        internal void SetVertices(List<Vector2> vertices)
        {
            DebugTools.Assert(vertices.Count == 4, "Vertices optimisation only usable on rectangles");

            // TODO: Get what the normals should be
            Vertices = vertices;
            // _normals = new List<Vector2>()
            // Verify on debug that the vertices skip was actually USEFUL
            DebugTools.Assert(Vertices == vertices);
        }

        public bool Equals(IPhysShape? other)
        {
            // TODO: Could use casts for AABB and Rect
            if (other is not PolygonShape poly) return false;
            if (_vertices.Count != poly.Vertices.Count) return false;
            for (var i = 0; i < _vertices.Count; i++)
            {
                var vert = _vertices[i];
                if (!vert.EqualsApprox(poly.Vertices[i])) return false;
            }

            return true;
        }

        public Box2 CalculateLocalBounds(Angle rotation)
        {
            if (Vertices.Count == 0) return new Box2();

            var aabb = new Box2();
            Vector2 lower = Vertices[0];
            Vector2 upper = lower;

            for (int i = 1; i < Vertices.Count; ++i)
            {
                Vector2 v = Vertices[i];
                lower = Vector2.ComponentMin(lower, v);
                upper = Vector2.ComponentMax(upper, v);
            }

            Vector2 r = new Vector2(Radius, Radius);
            aabb.BottomLeft = lower - r;
            aabb.TopRight = upper + r;

            return aabb;
        }

        public void ApplyState()
        {
            return;
        }

        public void DebugDraw(DebugDrawingHandle handle, in Matrix3 modelMatrix, in Box2 worldViewport, float sleepPercent)
        {
            var m = Matrix3.Identity;
            m.R0C2 = modelMatrix.R0C2;
            m.R1C2 = modelMatrix.R1C2;
            handle.SetTransform(m);
            handle.DrawPolygonShape(_vertices.ToArray(), handle.CalcWakeColor(handle.RectFillColor, sleepPercent));
            handle.SetTransform(Matrix3.Identity);
        }

        public static explicit operator PolygonShape(PhysShapeAabb aabb)
        {
            // TODO: Need a test for this.
            var bounds = aabb.LocalBounds;

            // Don't use Vertices property given we can just unwind it ourselves faster.
            // Ideal world we don't need this but for now.
            return new PolygonShape
            {
                // Giftwrap seems to use bottom-right first.
                _vertices = new List<Vector2>
                {
                    bounds.BottomRight,
                    bounds.TopRight,
                    bounds.TopLeft,
                    bounds.BottomLeft,
                },

                _normals = new List<Vector2>
                {
                    new(1, -0),
                    new (0, 1),
                    new (-1, -0),
                    new (0, -1),
                }
            };
        }

        public static explicit operator PolygonShape(PhysShapeRect rect)
        {
            // Ideal world we don't even need PhysShapeRect?
            var bounds = rect.CachedBounds;

            return new PolygonShape
            {
                _vertices = new List<Vector2>
                {
                    bounds.BottomRight,
                    bounds.TopRight,
                    bounds.TopLeft,
                    bounds.BottomLeft,
                },

                _normals = new List<Vector2>
                {
                    new(1, -0),
                    new (0, 1),
                    new (-1, -0),
                    new (0, -1),
                }
            };
        }
    }
}
