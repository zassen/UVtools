﻿/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */

using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace UVtools.Core.Gerber.Apertures;

public class CircleAperture : Aperture
{
    #region Properties
    public double Diameter { get; set; }
    #endregion

    #region Constructor
    public CircleAperture(GerberDocument document) : base(document, "Circle") { }

    public CircleAperture(GerberDocument document, int index, double diameter) : base(document, index, "Circle")
    {
        Diameter = document.GetMillimeters(diameter);
    }
    #endregion

    public override void DrawFlashD3(Mat mat, PointF at, MCvScalar color, LineType lineType = LineType.EightConnected)
    {
        CvInvoke.Circle(mat,
            Document.PositionMmToPx(at),
            Document.SizeMmToPx(Diameter / 2), color, -1, lineType);
    }
}