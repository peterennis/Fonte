﻿/**
 * Copyright 2018, Adrien Tétar. All Rights Reserved.
 */

namespace Fonte.App.Utilities
{
    using Fonte.Data.Collections;
    using PointType = Data.PointType;

    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    public enum SpecialBehavior
    {
        None = 0,
        // Keep off curves relative position on segment constant
        InterpolateSegment,
        // Move on curve along handles path
        LockHandles
    };

    class Outline
    {
        public static void BreakPath(Data.Path path, int index)
        {
            var points = path.Points;
            var point = points[index];
            point.Smooth = false;

            var after = points.GetRange(index, points.Count - index);
            if (path.IsOpen)
            {
                var otherPath = new Data.Path();
                otherPath.Points.Clear();
                otherPath.Points.AddRange(after);
                points.RemoveRange(index, points.Count - index);
                path.Parent.Paths.Add(otherPath);
            }
            else
            {
                var before = points.GetRange(0, index);
                points.Clear();
                points.AddRange(after);
                points.AddRange(before);
            }
            points.Add(point.Clone());
            point.Type = PointType.Move;
        }

        // XXX: impl more
        public static void DeleteSelection(Data.Layer layer, bool breakPaths = false)
        {
            List<Data.Path> paths;
            if (breakPaths)
            {
                paths = DeleteSelection(layer.Paths);
            }
            else
            {
                paths = MergeSelection(layer.Paths);
            }
            layer.Paths.Clear();
            layer.Paths.AddRange(paths);
        }

        public static void JoinPaths(Data.Path path, bool atStart, Data.Path otherPath, bool atOtherStart,
                                     bool mergeJoin = false)
        {
            if (path == otherPath)
            {
                if (atStart == atOtherStart) return;
                if (atStart)
                {
                    path.Reverse();
                }
                path.Close();
                if (mergeJoin)
                {
                    var duplPoint = path.Points.Pop();
                    // Here lastPoint might not have precisely the same position as the dupl. point we just pruned
                    var lastPoint = path.Points.Last();
                    lastPoint.X = duplPoint.X;
                    lastPoint.Y = duplPoint.Y;
                }
            }
            else
            {
                if (atStart)
                {
                    path.Reverse();
                }
                if (!atOtherStart)
                {
                    otherPath.Reverse();
                }
                var points = path.Points;
                PointType type;
                if (mergeJoin)
                {
                    type = points.Pop().Type;
                }
                else
                {
                    type = PointType.Line;
                }
                var layer = path.Parent;
                layer.ClearSelection();
                var otherPoints = otherPath.Points;
                points.AddRange(otherPoints);
                layer.Paths.Remove(otherPath);
                var otherFirstPoint = otherPoints[0];
                otherFirstPoint.Type = type;
                otherFirstPoint.Selected = true;
            }
        }

        public static void MoveSelection(Data.Path path, float dx, float dy, SpecialBehavior behavior = SpecialBehavior.None)
        {
            if (path.Points.Count < 2)
            {
                var lonePoint = path.Points.First();
                if (lonePoint.Selected)
                {
                    lonePoint.X += dx;
                    lonePoint.Y += dy;
                }
                return;
            }
            // First pass: move
            Data.Point prevOn = null;
            var prev = path.Points[path.Points.Count - 2];
            var point = path.Points.Last();
            foreach (var next in path.Points)
            {
                if (point.Selected)
                {
                    point.X += dx;
                    point.Y += dy;
                }
                if (behavior != SpecialBehavior.LockHandles)
                {
                    if (point.Type != PointType.None)
                    {
                        if (point.Selected)
                        {
                            if (prev.Type == PointType.None && !prev.Selected && point.Type != PointType.Move)
                            {
                                prev.X += dx;
                                prev.Y += dy;
                            }
                            if (next.Type == PointType.None && !next.Selected)
                            {
                                next.X += dx;
                                next.Y += dy;
                            }
                        }
                        prevOn = point;
                    }
                    if (behavior == SpecialBehavior.InterpolateSegment && next.Type == PointType.Curve)
                    {
                        // XXX: next hasn't moved yet
                        MoveSegmentInterpolate(prevOn, prev, point, next, dx, dy);
                    }
                }
                prev = point;
                point = next;
            }
            if (path.Points.Count < 3)
            {
                return;
            }

            // Second pass: project
            foreach (var next in path.Points)
            {
                var atNode = point.Type != PointType.None &&
                             prev.Type == PointType.None || next.Type == PointType.None;
                // Slide points
                if (atNode && behavior == SpecialBehavior.LockHandles)
                {
                    // TODO: make this a function instead of cloning local vars?
                    var (p1, p2, p3) = (prev, point, next);
                    if (p2.Selected && p2.Type != PointType.Move)
                    {
                        if (p1.Selected || p3.Selected)
                        {
                            if (p1.Selected)
                            {
                                (p1, p3) = (p3, p1);
                            }
                            if (!p1.Selected && p3.Type == PointType.None)
                            {
                                VectorRotation(p3, p1.ToVector2(), p2.ToVector2());
                            }
                        }
                        else
                        {
                            if (p2.Smooth)
                            {
                                VectorProjection(p2, p1.ToVector2(), p3.ToVector2());
                            }
                        }
                    }
                    else if (p1.Selected != p3.Selected)
                    {
                        if (p1.Selected)
                        {
                            (p1, p3) = (p3, p1);
                        }
                        if (p3.Type == PointType.None)
                        {
                            if (p2.Smooth && p2.Type != PointType.Move)
                            {
                                VectorProjection(p3, p1.ToVector2(), p2.ToVector2());
                            }
                            else
                            {
                                var rvec = new Vector2(p3.X - dx, p3.Y - dy);
                                VectorProjection(p3, p2.ToVector2(), rvec);
                            }
                        }
                    }
                }
                // Rotation/projection across smooth onCurve
                if (atNode && point.Smooth && point.Type != PointType.Move)
                {
                    // TODO: make this a function?
                    var (p1, p2, p3) = (prev, point, next);
                    if (p2.Selected)
                    {
                        if (p1.Type == PointType.None)
                        {
                            (p1, p3) = (p3, p1);
                        }
                        if (p1.Type != PointType.None)
                        {
                            VectorRotation(p3, p1.ToVector2(), p2.ToVector2());
                        }
                    }
                    else if (p1.Selected != p3.Selected)
                    {
                        if (p1.Selected)
                        {
                            (p1, p3) = (p3, p1);
                        }
                        if (p1.Type != PointType.None)
                        {
                            VectorProjection(p3, p1.ToVector2(), p2.ToVector2());
                        }
                        else if (behavior != SpecialBehavior.LockHandles)
                        {
                            VectorRotation(p1, p3.ToVector2(), p2.ToVector2());
                        }
                    }
                }
                // --
                prev = point;
                point = next;
            }
        }

        // TODO: take Vector2 for delta?
        // XXX: impl more
        public static void MoveSelection(Data.Layer layer, float dx, float dy, SpecialBehavior behavior = SpecialBehavior.None)
        {
            // XXX: for multi-level undo, add a PhantomChangeGroup
            //var group = layer.CreateUndoGroup();
            foreach (var path in layer.Paths)
            {
                MoveSelection(path, dx, dy, behavior);
            }
            //group.Dispose();
        }

        /**/

        static bool AnyOffCurveSelected(Data.Segment segment)
        {
            return Enumerable.Any(segment.OffCurves, offCurve => offCurve.Selected);
        }

        static List<Data.Path> DeleteSelection(IEnumerable<Data.Path> paths)
        {
            var outPaths = new List<Data.Path>();
            foreach (var path in Selection.FilterSelection(paths))
            {
                var segmentsList = new List<Data.Segment>(path.Segments);
                IEnumerable<Data.Segment> iter;
                if (!(path.IsOpen ||
                      AnyOffCurveSelected(segmentsList.First())
                      ))
                {
                    var firstIndex = segmentsList.Count;

                    for (int ix = segmentsList.Count - 1; ix >= 0; --ix)
                    {
                        firstIndex -= 1;
                        if (AnyOffCurveSelected(segmentsList[ix]))
                        {
                            break;
                        }
                    }
                    if (firstIndex == 0)
                    {
                        // None selected, bring on the original path
                        outPaths.Add(path);
                        continue;
                    }
                    else
                    {
                        firstIndex += segmentsList.Count;
                        iter = Iterable.IterAt(segmentsList, firstIndex);
                    }
                }
                else
                {
                    iter = segmentsList;
                }

                Data.Path outPath = null;
                foreach (var segment in iter)
                {
                    if (AnyOffCurveSelected(segment) || segment.OnCurve.Type == PointType.Move)
                    {
                        if (outPath != null)
                        {
                            outPaths.Add(outPath);
                        }
                        outPath = new Data.Path();
                        var point = segment.OnCurve;
                        point.Smooth = false;
                        point.Type = PointType.Move;
                        outPath.Points.Add(point);
                    }
                    else
                    {
                        outPath.Points.AddRange(segment.Points);
                    }
                }
            }

            return outPaths;
        }

        static List<Data.Path> MergeSelection(IEnumerable<Data.Path> paths)
        {
            var outPaths = new List<Data.Path>();
            foreach (var path in paths)
            {
                var segments = new List<Data.Segment>(path.Segments);
                var forwardMove = false;
                for (int ix = segments.Count - 1; ix >= 0; --ix)
                {
                    var segment = segments[ix];
                    if (segment.OnCurve.Selected)
                    {
                        if (ix == 0 && segment.OnCurve.Type == PointType.Move)
                        {
                            forwardMove = true;
                        }
                        segments.RemoveAt(ix);
                    }
                    else if (AnyOffCurveSelected(segment))
                    {
                        segment.ConvertTo(PointType.Line);
                    }
                }

                if (segments.Count > 0)
                {
                    if (forwardMove)
                    {
                        segments[0].ConvertTo(PointType.Move);
                    }
                    outPaths.Add(path);
                }
            }

            return outPaths;
        }

        static void MoveSegmentInterpolate(Data.Point on1, Data.Point off1, Data.Point off2, Data.Point on2, float dx, float dy)
        {
            if (on2.Selected != on1.Selected)
            {
                var sign = on1.Selected ? -1 : 1;
                var sdelta = new Vector2(sign * dx, sign * dy);

                var ondelta = on2.ToVector2() - on1.ToVector2();
                var factor = ondelta - sdelta;
                if (factor.X != 0 && factor.Y != 0)
                {
                    factor = ondelta / factor;
                }

                if (!off1.Selected)
                {
                    off1.X = on1.X + factor.X * (off1.X - on1.X);
                    off1.Y = on1.Y + factor.Y * (off1.Y - on1.Y);
                }
                if (!off2.Selected)
                {
                    off2.X = on1.X + factor.X * (off2.X - on1.X - sdelta.X);
                    off2.Y = on1.Y + factor.Y * (off2.Y - on1.Y - sdelta.Y);
                }
            }
        }

        static void VectorProjection(Data.Point point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var l2 = ab.LengthSquared();
            if (l2 != 0)
            {
                var ap = point.ToVector2() - a;
                var t = Vector2.Dot(ap, ab) / l2;

                point.X = a.X + t * ab.X;
                point.Y = a.Y + t * ab.Y;
            }
            else
            {
                point.X = a.X;
                point.Y = a.Y;
            }
        }

        static void VectorRotation(Data.Point point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var ab_len = ab.Length();
            if (ab_len != 0)
            {
                var p = point.ToVector2();
                var pb_len = (b - p).Length();
                var t = (ab_len + pb_len) / ab_len;

                point.X = a.X + t * ab.X;
                point.Y = a.Y + t * ab.Y;
            }
        }
    }
}
