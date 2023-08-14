// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Rulesets.Objects
{
    public static class SliderPathExtensions
    {
        /// <summary>
        /// Snaps the provided <paramref name="hitObject"/>'s duration using the <paramref name="snapProvider"/>.
        /// </summary>
        public static void SnapTo<THitObject>(this THitObject hitObject, IDistanceSnapProvider? snapProvider)
            where THitObject : HitObject, IHasPath
        {
            hitObject.Path.ExpectedDistance.Value = snapProvider?.FindSnappedDistance(hitObject, (float)hitObject.Path.CalculatedDistance) ?? hitObject.Path.CalculatedDistance;
        }

        /// <summary>
        /// Reverse the direction of this path.
        /// </summary>
        /// <param name="sliderPath">The <see cref="SliderPath"/>.</param>
        /// <param name="positionalOffset">The positional offset of the resulting path. It should be added to the start position of this path.</param>
        public static void Reverse(this SliderPath sliderPath, out Vector2 positionalOffset)
        {
            var controlPoints = sliderPath.ControlPoints;
            var originalControlPointTypes = controlPoints.Select(p => p.Type).ToArray();

            controlPoints[0].Type ??= PathType.Linear;

            // Inherited points after a linear point should be treated as linear points.
            controlPoints.Where(p => sliderPath.PointsInSegment(p)[0].Type == PathType.Linear).ForEach(p => p.Type = PathType.Linear);

            double[] segmentEnds = sliderPath.GetSegmentEnds().ToArray();
            double[] distinctSegmentEnds = segmentEnds.Distinct().ToArray();

            // Remove control points at the end which do not affect the visual slider path ("invisible" control points).
            if (segmentEnds.Length >= 2 && Math.Abs(segmentEnds[^1] - segmentEnds[^2]) < 1e-10 && distinctSegmentEnds.Length > 1)
            {
                int numVisibleSegments = distinctSegmentEnds.Length - 2;
                var nonInheritedControlPoints = controlPoints.Where(p => p.Type is not null).ToList();

                int lastVisibleControlPointIndex = controlPoints.IndexOf(nonInheritedControlPoints[numVisibleSegments]);

                // Make sure to include all inherited control points directly after the last visible non-inherited control point.
                while (lastVisibleControlPointIndex + 1 < controlPoints.Count)
                {
                    lastVisibleControlPointIndex++;

                    if (controlPoints[lastVisibleControlPointIndex].Type is not null)
                        break;
                }

                // Remove all control points after the first invisible non-inherited control point.
                controlPoints.RemoveRange(lastVisibleControlPointIndex + 1, controlPoints.Count - lastVisibleControlPointIndex - 1);
            }

            // Restore original control point types.
            controlPoints.Zip(originalControlPointTypes).ForEach(x => x.First.Type = x.Second);

            // Recalculate perfect curve at the end of the slider path.
            if (controlPoints.Count >= 3 && controlPoints[^3].Type == PathType.PerfectCurve && controlPoints[^2].Type is null && distinctSegmentEnds.Length > 1)
            {
                double lastSegmentStart = distinctSegmentEnds[^2];
                double lastSegmentEnd = distinctSegmentEnds[^1];

                var circleArcPath = new List<Vector2>();
                sliderPath.GetPathToProgress(circleArcPath, lastSegmentStart / lastSegmentEnd, 1);

                controlPoints[^2].Position = circleArcPath[circleArcPath.Count / 2];
            }

            sliderPath.reverseControlPoints(out positionalOffset);
        }

        /// <summary>
        /// Reverses the order of the provided <see cref="SliderPath"/>'s <see cref="PathControlPoint"/>s.
        /// </summary>
        /// <param name="sliderPath">The <see cref="SliderPath"/>.</param>
        /// <param name="positionalOffset">The positional offset of the resulting path. It should be added to the start position of this path.</param>
        private static void reverseControlPoints(this SliderPath sliderPath, out Vector2 positionalOffset)
        {
            var points = sliderPath.ControlPoints.ToArray();
            positionalOffset = sliderPath.PositionAt(1);

            sliderPath.ControlPoints.Clear();

            PathType? lastType = null;

            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                p.Position -= positionalOffset;

                // propagate types forwards to last null type
                if (i == points.Length - 1)
                {
                    p.Type = lastType;
                    p.Position = Vector2.Zero;
                }
                else if (p.Type != null)
                    (p.Type, lastType) = (lastType, p.Type);

                sliderPath.ControlPoints.Insert(0, p);
            }
        }
    }
}
