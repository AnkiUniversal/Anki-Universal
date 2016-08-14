//The MIT License(MIT) Copyright(c) 2012 Filip Skakun

//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

// Ported from Silverlight Toolkit.
// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.


using System.Runtime.InteropServices;
using Windows.UI.Xaml.Controls;


namespace AnkiU.UIUtilities
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct OrientedSize
    {
        /// <summary>
        /// The orientation of the structure.
        /// </summary>
        private Orientation _orientation;

        /// <summary>
        /// Gets the orientation of the structure.
        /// </summary>
        public Orientation Orientation
        {
            get { return _orientation; }
        }

        /// <summary>
        /// The size dimension that grows directly with layout placement.
        /// </summary>
        private double _direct;

        /// <summary>
        /// Gets or sets the size dimension that grows directly with layout
        /// placement.
        /// </summary>
        public double Direct
        {
            get { return _direct; }
            set { _direct = value; }
        }

        /// <summary>
        /// The size dimension that grows indirectly with the maximum value of
        /// the layout row or column.
        /// </summary>
        private double _indirect;

        /// <summary>
        /// Gets or sets the size dimension that grows indirectly with the
        /// maximum value of the layout row or column.
        /// </summary>
        public double Indirect
        {
            get { return _indirect; }
            set { _indirect = value; }
        }

        /// <summary>
        /// Gets or sets the width of the size.
        /// </summary>
        public double Width
        {
            get
            {
                return (Orientation == Orientation.Horizontal) ?
                    Direct :
                    Indirect;
            }
            set
            {
                if (Orientation == Orientation.Horizontal)
                {
                    Direct = value;
                }
                else
                {
                    Indirect = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the height of the size.
        /// </summary>
        public double Height
        {
            get
            {
                return (Orientation != Orientation.Horizontal) ?
                    Direct :
                    Indirect;
            }
            set
            {
                if (Orientation != Orientation.Horizontal)
                {
                    Direct = value;
                }
                else
                {
                    Indirect = value;
                }
            }
        }

        /// <summary>
        /// Initializes a new OrientedSize structure.
        /// </summary>
        /// <param name="orientation">Orientation of the structure.</param>
        public OrientedSize(Orientation orientation) :
            this(orientation, 0.0, 0.0)
        {
        }

        /// <summary>
        /// Initializes a new OrientedSize structure.
        /// </summary>
        /// <param name="orientation">Orientation of the structure.</param>
        /// <param name="width">Un-oriented width of the structure.</param>
        /// <param name="height">Un-oriented height of the structure.</param>
        public OrientedSize(Orientation orientation, double width, double height)
        {
            _orientation = orientation;

            // All fields must be initialized before we access the this pointer
            _direct = 0.0;
            _indirect = 0.0;

            Width = width;
            Height = height;
        }
    }
}
