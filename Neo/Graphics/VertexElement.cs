﻿using System;

namespace Neo.Graphics
{
	public enum DataType
    {
        Float,
        Byte
    }

	public class VertexElement
    {
        private readonly InputElement mDescription;

        public InputElement Element { get { return this.mDescription; } }

        public VertexElement(string semantic, int index, int components, DataType dataType = DataType.Float, bool normalized = false, int slot = 0, bool instanceData = false)
        {
	        this.mDescription = new InputElement
            {
                AlignedByteOffset = InputElement.AppendAligned,
                Classification = instanceData ? InputClassification.PerInstanceData : InputClassification.PerVertexData,
                InstanceDataStepRate = instanceData ? 1 : 0,
                SemanticIndex = index,
                SemanticName = semantic,
                Slot = slot
            };

            if(dataType == DataType.Byte)
            {
                switch(components)
                {
                    case 1:
	                    this.mDescription.Format = normalized ? Format.R8_UNorm : Format.R8_UInt;
                        break;

                    case 2:
	                    this.mDescription.Format = normalized ? Format.R8G8_UNorm : Format.R8G8_UInt;
                        break;

                    case 4:
	                    this.mDescription.Format = normalized ? Format.R8G8B8A8_UNorm : Format.R8G8B8A8_UInt;
                        break;

                    default:
                        throw new ArgumentException("Invalid combination of data type and component count");
                }
            }
            else
            {
                switch(components)
                {
                    case 1:
	                    this.mDescription.Format = Format.R32_Float;
                        break;

                    case 2:
	                    this.mDescription.Format = Format.R32G32_Float;
                        break;

                    case 3:
	                    this.mDescription.Format = Format.R32G32B32_Float;
                        break;

                    case 4:
	                    this.mDescription.Format = Format.R32G32B32A32_Float;
                        break;

                    default:
                        throw new ArgumentException("Invalid combination of data type and component count");
                }
            }
        }
    }
}