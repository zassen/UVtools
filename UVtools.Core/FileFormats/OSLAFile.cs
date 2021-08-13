﻿/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */

// https://github.com/sn4k3/UVtools/blob/master/Documentation/osla.md

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BinarySerialization;
using Emgu.CV;
using Emgu.CV.CvEnum;
using MoreLinq;
using UVtools.Core.Extensions;
using UVtools.Core.GCode;
using UVtools.Core.Operations;

namespace UVtools.Core.FileFormats
{
    public class OSLAFile : FileFormat
    {
        #region Constants

        public const string MARKER = "OSLATiCo";
        #endregion

        #region Sub Classes
        #region Header
        public class FileDef
        {
            [FieldOrder(0)]
            [FieldLength(8)]
            public string Marker { get; set; } = MARKER;

            [FieldOrder(1)]
            public ushort Version { get; set; } = 0;

            [FieldOrder(2)]
            [FieldLength(20)]
            public string CreatedDateTime { get; set; } = DateTime.UtcNow.ToString("u");

            [FieldOrder(3)]
            [FieldLength(50)]
            [SerializeAs(SerializedType.TerminatedString)]
            public string CreatedBy { get; set; } = About.SoftwareWithVersion;

            [FieldOrder(4)]
            [FieldLength(20)]
            public string ModifiedDateTime { get; set; } = DateTime.UtcNow.ToString("u");

            [FieldOrder(5)]
            [FieldLength(50)]
            [SerializeAs(SerializedType.TerminatedString)]
            public string ModifiedBy { get; set; } = About.SoftwareWithVersion;

            public override string ToString()
            {
                return $"{nameof(Marker)}: {Marker}, {nameof(Version)}: {Version}, {nameof(CreatedDateTime)}: {CreatedDateTime}, {nameof(CreatedBy)}: {CreatedBy}, {nameof(ModifiedDateTime)}: {ModifiedDateTime}, {nameof(ModifiedBy)}: {ModifiedBy}";
            }

            public void Update()
            {
                ModifiedDateTime= DateTime.UtcNow.ToString("u");
                ModifiedBy = About.SoftwareWithVersion;;
            }

            public void Validate()
            {
                if (Marker != MARKER)
                {
                    throw new FileLoadException($"Invalid marker: {Marker}, not a valid OSLA file.");
                }
            }
        }


        public class Header
        {
            [FieldOrder(0)] public uint TableSize { get; set; }
            [FieldOrder(1)] public uint ResolutionX { get; set; }
            [FieldOrder(2)] public uint ResolutionY { get; set; }
            [FieldOrder(3)] public float MachineZ { get; set; }
            [FieldOrder(4)] public float DisplayWidth { get; set; }
            [FieldOrder(5)] public float DisplayHeight { get; set; }
            [FieldOrder(6)] public byte DisplayMirror { get; set; } // 0 = No mirror | 1 = Horizontally | 2 = Vertically | 3 = Horizontally+Vertically | >3 = No mirror
            [FieldOrder(7)] [FieldLength(16)] [SerializeAs(SerializedType.TerminatedString)] public string PreviewDataType { get; set; } = "RGB565";
            [FieldOrder(8)] [FieldLength(16)] [SerializeAs(SerializedType.TerminatedString)] public string LayerDataType { get; set; } = "PNG";
            [FieldOrder(9)] public uint PreviewTableSize { get; set; } = 8;
            [FieldOrder(10)] public uint PreviewCount { get; set; }
            [FieldOrder(11)] public float LayerHeight { get; set; } = 0.05f;
            [FieldOrder(12)] public ushort BottomLayersCount { get; set; } = 4;
            [FieldOrder(13)] public uint LayerCount { get; set; }
            [FieldOrder(14)] public uint LayerTableSize { get; set; } = 69;
            [FieldOrder(15)] public uint LayerDefinitionsAddress { get; set; }
            [FieldOrder(16)] public uint GCodeAddress { get; set; }
            [FieldOrder(17)] public uint PrintTime { get; set; }
            [FieldOrder(18)] public float MaterialMilliliters { get; set; }
            [FieldOrder(19)] public float MaterialCost { get; set; }
            [FieldOrder(20)] [FieldLength(50)] [SerializeAs(SerializedType.TerminatedString)] public string MaterialName { get; set; }
            [FieldOrder(21)] [FieldLength(50)] [SerializeAs(SerializedType.TerminatedString)] public string MachineName { get; set; } = "Unknown";


            public override string ToString()
            {
                return $"{nameof(TableSize)}: {TableSize}, {nameof(ResolutionX)}: {ResolutionX}, {nameof(ResolutionY)}: {ResolutionY}, {nameof(MachineZ)}: {MachineZ}, {nameof(DisplayWidth)}: {DisplayWidth}, {nameof(DisplayHeight)}: {DisplayHeight}, {nameof(DisplayMirror)}: {DisplayMirror}, {nameof(PreviewDataType)}: {PreviewDataType}, {nameof(LayerDataType)}: {LayerDataType}, {nameof(PreviewTableSize)}: {PreviewTableSize}, {nameof(PreviewCount)}: {PreviewCount}, {nameof(LayerTableSize)}: {LayerTableSize}, {nameof(BottomLayersCount)}: {BottomLayersCount}, {nameof(LayerCount)}: {LayerCount}, {nameof(LayerDefinitionsAddress)}: {LayerDefinitionsAddress}, {nameof(GCodeAddress)}: {GCodeAddress}, {nameof(PrintTime)}: {PrintTime}, {nameof(MaterialMilliliters)}: {MaterialMilliliters}, {nameof(MaterialCost)}: {MaterialCost}, {nameof(MaterialName)}: {MaterialName}, {nameof(MachineName)}: {MachineName}";
            }
        }
        #endregion

        #region Custom Table

        public class CustomTable
        {
            [FieldOrder(0)] public uint TableSize { get; set; }

            [FieldOrder(1)] [FieldCount(nameof(TableSize))] public byte[] Bytes { get; set; } = Array.Empty<byte>();

            public override string ToString()
            {
                return $"{nameof(TableSize)}: {TableSize}, {nameof(Bytes)}: {Bytes.Length}";
            }
        }

        #endregion

        #region Preview
        public class Preview
        {
            /// <summary>
            /// Gets the X dimension of the preview image, in pixels. 
            /// </summary>
            [FieldOrder(0)] public ushort ResolutionX { get; set; }

            /// <summary>
            /// Gets the Y dimension of the preview image, in pixels. 
            /// </summary>
            [FieldOrder(1)] public ushort ResolutionY { get; set; }

            /// <summary>
            /// Gets the image length in bytes.
            /// </summary>
            [FieldOrder(2)] public uint ImageLength { get; set; }
            //[FieldOrder(3)] [FieldCount(nameof(ImageLength))] public byte[] ImageData { get; set; }


            public override string ToString()
            {
                return $"{nameof(ResolutionX)}: {ResolutionX}, {nameof(ResolutionY)}: {ResolutionY}, {nameof(ImageLength)}: {ImageLength}";
            }
        }

        #endregion

        #region Layer
        public class LayerDef
        {
            //[FieldOrder(0)] public uint DataAddress { get; set; }
            
            [FieldOrder(1)] public float PositionZ { get; set; }
            [FieldOrder(2)] public float LiftHeight { get; set; }
            [FieldOrder(3)] public float LiftSpeed { get; set; }
            [FieldOrder(4)] public float LiftHeight2 { get; set; }
            [FieldOrder(5)] public float LiftSpeed2 { get; set; }
            [FieldOrder(6)] public float WaitTimeAfterLift { get; set; }
            [FieldOrder(7)] public float RetractSpeed { get; set; }
            [FieldOrder(8)] public float RetractHeight2 { get; set; }
            [FieldOrder(9)] public float RetractSpeed2 { get; set; }
            [FieldOrder(10)] public float WaitTimeBeforeCure { get; set; }
            [FieldOrder(11)] public float ExposureTime { get; set; }
            [FieldOrder(12)] public float WaitTimeAfterCure { get; set; }
            [FieldOrder(13)] public byte LightPWM { get; set; }
            [FieldOrder(14)] public uint BoundingRectangleX { get; set; }
            [FieldOrder(15)] public uint BoundingRectangleY { get; set; }
            [FieldOrder(16)] public uint BoundingRectangleWidth { get; set; }
            [FieldOrder(17)] public uint BoundingRectangleHeight { get; set; }

            //[Ignore] public byte[] ImageData { get; set; }

            public LayerDef()
            {
            }

            public LayerDef(Layer layer)
            {
                PositionZ = layer.PositionZ;
                LiftHeight = layer.LiftHeight;
                LiftSpeed = layer.LiftSpeed;
                LiftHeight2 = layer.LiftHeight2;
                LiftSpeed2 = layer.LiftSpeed2;
                WaitTimeAfterLift = layer.WaitTimeAfterLift;
                RetractSpeed = layer.RetractSpeed;
                RetractHeight2 = layer.RetractHeight2;
                RetractSpeed2 = layer.RetractSpeed2;
                WaitTimeBeforeCure = layer.WaitTimeBeforeCure;
                ExposureTime = layer.ExposureTime;
                WaitTimeAfterCure = layer.WaitTimeAfterCure;
                LightPWM = layer.LightPWM;
                BoundingRectangleX = (uint)layer.BoundingRectangle.X;
                BoundingRectangleY = (uint)layer.BoundingRectangle.Y;
                BoundingRectangleWidth =  (uint)layer.BoundingRectangle.Width;
                BoundingRectangleHeight = (uint)layer.BoundingRectangle.Height;
            }

            public void SetTo(Layer layer)
            {
                layer.PositionZ = PositionZ;
                layer.LiftHeight = LiftHeight;
                layer.LiftSpeed = LiftSpeed;
                layer.LiftHeight2 = LiftHeight2;
                layer.LiftSpeed2 = LiftSpeed2;
                layer.WaitTimeAfterLift = WaitTimeAfterLift;
                layer.RetractSpeed = RetractSpeed;
                layer.RetractHeight2 = RetractHeight2;
                layer.RetractSpeed2 = RetractSpeed2;
                layer.WaitTimeBeforeCure = WaitTimeBeforeCure;
                layer.ExposureTime = ExposureTime;
                layer.WaitTimeAfterCure = WaitTimeAfterCure;
                layer.LightPWM = LightPWM;
            }
        }
        #endregion

        #region GCode

        public class GCodeDef
        {
            [FieldOrder(0)]
            public uint GCodeSize { get; set; }

            [FieldOrder(1)] [FieldLength(nameof(GCodeSize))]
            public string GCodeText { get; set; }
        }
        #endregion

        #endregion

        #region Properties

        public FileDef FileSettings { get; protected internal set; } = new();
        public Header HeaderSettings { get; protected internal set; } = new();
        public CustomTable CustomTableSettings { get; protected internal set; } = new();
        public Preview[] Previews { get; protected internal set; }

        public override FileFormatType FileType => FileFormatType.Binary;

        public override FileExtension[] FileExtensions { get; } = {
            new (typeof(OSLAFile), "osla", "Open SLA universal binary file"),
            //new ("omsla", "Open mSLA universal binary file"),
            //new ("odlp", "Open DLP universal binary file"),
        };

        public override PrintParameterModifier[] PrintParameterModifiers { get; } = {
            PrintParameterModifier.BottomLayerCount,

            PrintParameterModifier.BottomWaitTimeBeforeCure,
            PrintParameterModifier.WaitTimeBeforeCure,

            PrintParameterModifier.BottomExposureTime,
            PrintParameterModifier.ExposureTime,

            PrintParameterModifier.BottomWaitTimeAfterCure,
            PrintParameterModifier.WaitTimeAfterCure,

            PrintParameterModifier.BottomLiftHeight,
            PrintParameterModifier.BottomLiftSpeed,
            PrintParameterModifier.LiftHeight,
            PrintParameterModifier.LiftSpeed,

            PrintParameterModifier.BottomLiftHeight2,
            PrintParameterModifier.BottomLiftSpeed2,
            PrintParameterModifier.LiftHeight2,
            PrintParameterModifier.LiftSpeed2,

            PrintParameterModifier.BottomWaitTimeAfterLift,
            PrintParameterModifier.WaitTimeAfterLift,

            PrintParameterModifier.BottomRetractSpeed,
            PrintParameterModifier.RetractSpeed,

            PrintParameterModifier.BottomRetractHeight2,
            PrintParameterModifier.BottomRetractSpeed2,
            PrintParameterModifier.RetractHeight2,
            PrintParameterModifier.RetractSpeed2,

            PrintParameterModifier.BottomLightPWM,
            PrintParameterModifier.LightPWM,
        };

        public override PrintParameterModifier[] PrintParameterPerLayerModifiers { get; } = {
            PrintParameterModifier.WaitTimeBeforeCure,
            PrintParameterModifier.ExposureTime,
            PrintParameterModifier.WaitTimeAfterCure,
            PrintParameterModifier.LiftHeight,
            PrintParameterModifier.LiftSpeed,
            PrintParameterModifier.LiftHeight2,
            PrintParameterModifier.LiftSpeed2,
            PrintParameterModifier.WaitTimeAfterLift,
            PrintParameterModifier.RetractSpeed,
            PrintParameterModifier.RetractHeight2,
            PrintParameterModifier.RetractSpeed2,
            PrintParameterModifier.LightPWM,
        };

        public override Size[] ThumbnailsOriginalSize { get; } =
        {
            new(400, 400), 
            new(200, 200)
        };

        public override uint ResolutionX
        {
            get => HeaderSettings.ResolutionX;
            set
            {
                HeaderSettings.ResolutionX = value;
                RaisePropertyChanged();
            }
        }

        public override uint ResolutionY
        {
            get => HeaderSettings.ResolutionY;
            set
            {
                HeaderSettings.ResolutionY = value;
                RaisePropertyChanged();
            }
        }

        public override float DisplayWidth
        {
            get => HeaderSettings.DisplayWidth;
            set
            {
                HeaderSettings.DisplayWidth = (float)Math.Round(value, 2);
                RaisePropertyChanged();
            }
        }


        public override float DisplayHeight
        {
            get => HeaderSettings.DisplayHeight;
            set
            {
                HeaderSettings.DisplayHeight = (float)Math.Round(value, 2);
                RaisePropertyChanged();
            }
        }

        public override float MachineZ
        {
            get => HeaderSettings.MachineZ > 0 ? HeaderSettings.MachineZ : base.MachineZ;
            set
            {
                HeaderSettings.MachineZ = (float)Math.Round(value, 2);
                RaisePropertyChanged();
            }
        }

        public override bool DisplayMirror
        {
            get => HeaderSettings.DisplayMirror is >= 1 and <= 3;
            set
            {
                HeaderSettings.DisplayMirror = value ? (byte)1 : (byte)0;
                RaisePropertyChanged();
            }
        }

        public override byte AntiAliasing
        {
            get => 8;
            set => RaisePropertyChanged();
        }

        public override float LayerHeight
        {
            get => HeaderSettings.LayerHeight;
            set
            {
                HeaderSettings.LayerHeight = Layer.RoundHeight(value);
                RaisePropertyChanged();
            }
        }

        public override uint LayerCount
        {
            get => base.LayerCount;
            set => HeaderSettings.LayerCount = base.LayerCount;
        }

        public override ushort BottomLayerCount
        {
            get => HeaderSettings.BottomLayersCount;
            set => base.BottomLayerCount = HeaderSettings.BottomLayersCount = value;
        }

        public override float PrintTime
        {
            get => base.PrintTime;
            set
            {
                base.PrintTime = value;
                HeaderSettings.PrintTime = (uint)base.PrintTime;
            }
        }

        public override float MaterialMilliliters
        {
            get => base.MaterialMilliliters;
            set
            {
                base.MaterialMilliliters = value;
                HeaderSettings.MaterialMilliliters = base.MaterialMilliliters;
            }
        }

        public override float MaterialCost
        {
            get => (float) Math.Round(HeaderSettings.MaterialCost, 3);
            set => base.MaterialCost = HeaderSettings.MaterialCost = (float)Math.Round(value, 3);
        }

        public override string MaterialName
        {
            get => HeaderSettings.MaterialName;
            set => base.MaterialName = HeaderSettings.MaterialName = value;
        }

        public override string MachineName
        {
            get => HeaderSettings.MachineName;
            set => base.MachineName = HeaderSettings.MachineName = value;
        }

        public override object[] Configs => new object[] { FileSettings, HeaderSettings };

        #endregion

        #region Constructors
        public OSLAFile()
        {
            //Previews = new Preview[ThumbnailsCount];
            GCode = new GCodeBuilder
            {
                UseTailComma = true,
                UseComments = true,
                GCodePositioningType = GCodeBuilder.GCodePositioningTypes.Absolute,
                GCodeSpeedUnit = GCodeBuilder.GCodeSpeedUnits.MillimetersPerMinute,
                GCodeTimeUnit = GCodeBuilder.GCodeTimeUnits.Milliseconds,
                GCodeShowImageType = GCodeBuilder.GCodeShowImageTypes.LayerIndex0Started,
                LayerMoveCommand = GCodeBuilder.GCodeMoveCommands.G0,
                EndGCodeMoveCommand = GCodeBuilder.GCodeMoveCommands.G0,
                CommandShowImageM6054 = {Arguments = "{0}"},
            };
        }
        #endregion

        #region Methods
        public override void Clear()
        {
            base.Clear();

            Previews = null;
        }

        protected override void EncodeInternally(string fileFullPath, OperationProgress progress)
        {
            using var outputFile = new FileStream(fileFullPath, FileMode.Create, FileAccess.Write);
            FileSettings.Update();
            var fileDefSize = Helpers.SerializeWriteFileStream(outputFile, FileSettings);
            HeaderSettings.TableSize = (uint)Helpers.Serializer.SizeOf(HeaderSettings);

            outputFile.Seek((int)HeaderSettings.TableSize, SeekOrigin.Current);

            Helpers.SerializeWriteFileStream(outputFile, CustomTableSettings); // Custom table

            // Previews
            progress.Reset(OperationProgress.StatusEncodePreviews, ThumbnailsCount);
            HeaderSettings.PreviewCount = 0;
            uint sizeofPreview = 0;
            for (byte i = 0; i < ThumbnailsCount; i++)
            {
                var image = Thumbnails[i];
                if(image is null) continue;

                progress.Token.ThrowIfCancellationRequested();

                var bytes = EncodeImage(image, HeaderSettings.PreviewDataType);
                if (bytes.Length == 0) continue;
                var preview = new Preview
                {
                    ResolutionX = (ushort) image.Width,
                    ResolutionY = (ushort) image.Height,
                    ImageLength = (uint) bytes.Length,
                };

                if (sizeofPreview == 0)
                {
                    sizeofPreview = (uint) Helpers.Serializer.SizeOf(preview);
                }

                HeaderSettings.PreviewCount++;

                Helpers.SerializeWriteFileStream(outputFile, preview);
                // Need to fill what we don't know
                if (HeaderSettings.PreviewTableSize > sizeofPreview)
                {
                    outputFile.Seek(HeaderSettings.LayerTableSize - sizeofPreview, SeekOrigin.Current);
                }
                outputFile.WriteBytes(bytes);
                
                progress++;
            }

            uint[] layerDataAddresses = new uint[LayerCount];
            progress.Reset(OperationProgress.StatusEncodeLayers, LayerCount);
            HeaderSettings.LayerDefinitionsAddress = (uint) outputFile.Position;
            
            outputFile.Seek(HeaderSettings.LayerTableSize * LayerCount, SeekOrigin.Current); // Start of layer data

            var layerHash = new Dictionary<string, uint>();
            
            var range = Enumerable.Range(0, (int)LayerCount);
            foreach (var batch in range.Batch(Environment.ProcessorCount * 10))
            {
                var layerBytes = new byte[LayerCount][];

                Parallel.ForEach(batch, layerIndex =>
                {
                    if (progress.Token.IsCancellationRequested) return;
                    using var mat = this[layerIndex].LayerMat;
                    layerBytes[layerIndex] = EncodeImage(mat, HeaderSettings.LayerDataType);
                    progress.LockAndIncrement();
                });

                foreach (var layerIndex in batch)
                {
                    progress.Token.ThrowIfCancellationRequested();

                    // Try to reuse layers
                    var hash = Helpers.ComputeSHA1Hash(layerBytes[layerIndex]);
                    if (layerHash.TryGetValue(hash, out var address))
                    {
                        layerDataAddresses[layerIndex] = address;
                    }
                    else
                    {
                        layerDataAddresses[layerIndex] = (uint)outputFile.Position;
                        outputFile.WriteUIntLittleEndian((uint)layerBytes[layerIndex].Length);
                        outputFile.WriteBytes(layerBytes[layerIndex]);
                        layerHash.Add(hash, layerDataAddresses[layerIndex]);
                    }

                    layerBytes[layerIndex] = null; // Clean
                }
            }

            HeaderSettings.GCodeAddress = (uint)outputFile.Position;

            uint layerTableSize = 0;
            outputFile.Seek(HeaderSettings.LayerDefinitionsAddress, SeekOrigin.Begin);
            for (int layerIndex = 0; layerIndex < layerDataAddresses.Length; layerIndex++)
            {
                progress.Token.ThrowIfCancellationRequested();

                var layer = this[layerIndex];
                var layerdef = new LayerDef(layer);
                
                outputFile.WriteUIntLittleEndian(layerDataAddresses[layerIndex]);
                Helpers.SerializeWriteFileStream(outputFile, layerdef);
                if (layerTableSize == 0)
                {
                    layerTableSize = 4 + (uint)Helpers.Serializer.SizeOf(layerdef);
                }
                if (HeaderSettings.LayerTableSize > layerTableSize)
                {
                    outputFile.Seek(HeaderSettings.LayerTableSize - layerTableSize, SeekOrigin.Current);
                }
            }

            progress.Reset(OperationProgress.StatusEncodeGcode);
            outputFile.Seek(HeaderSettings.GCodeAddress, SeekOrigin.Begin);
            RebuildGCode();
            var gcodeSettings = new GCodeDef { GCodeText = GCodeStr };
            gcodeSettings.GCodeSize = (uint)gcodeSettings.GCodeText.Length;
            Helpers.SerializeWriteFileStream(outputFile, gcodeSettings);

            outputFile.Seek(fileDefSize, SeekOrigin.Begin);
            Helpers.SerializeWriteFileStream(outputFile, HeaderSettings);

            Debug.WriteLine("Encode Results:");
            Debug.WriteLine(FileSettings);
            Debug.WriteLine(HeaderSettings);
            Debug.WriteLine("-End-");
        }

        protected override void DecodeInternally(string fileFullPath, OperationProgress progress)
        {
            using var inputFile = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read);
            FileSettings = Helpers.Deserialize<FileDef>(inputFile);
            Debug.Write("File -> ");
            Debug.WriteLine(FileSettings);
            FileSettings.Validate();

            HeaderSettings = Helpers.Deserialize<Header>(inputFile);
            Debug.Write("Header -> ");
            Debug.WriteLine(HeaderSettings);

            var headerSize = Helpers.Serializer.SizeOf(HeaderSettings);
            if (HeaderSettings.TableSize > headerSize) // By pass what we dont know
            {
                inputFile.Seek(HeaderSettings.TableSize - headerSize, SeekOrigin.Current);
            }

            CustomTableSettings = Helpers.Deserialize<CustomTable>(inputFile);
            Debug.Write("Custom table -> ");
            Debug.WriteLine(CustomTableSettings);

            progress.Reset(OperationProgress.StatusDecodePreviews, ThumbnailsCount);

            Previews = new Preview[HeaderSettings.PreviewCount];
            for (byte i = 0; i < HeaderSettings.PreviewCount; i++)
            {
                progress.Token.ThrowIfCancellationRequested();
                Previews[i] = Helpers.Deserialize<Preview>(inputFile);

                Debug.Write($"Preview {i} -> ");
                Debug.WriteLine(Previews[i]);

                // Need to fill what we don't know
                if (HeaderSettings.PreviewTableSize > 8)
                {
                    inputFile.Seek(HeaderSettings.LayerTableSize - 8, SeekOrigin.Current);
                }


                var bytes = inputFile.ReadBytes((int)Previews[i].ImageLength);

                Thumbnails[i] = DecodeImage(bytes, HeaderSettings.PreviewDataType, Previews[i].ResolutionX, Previews[i].ResolutionY);
                progress++;
            }

            inputFile.Seek(HeaderSettings.LayerDefinitionsAddress, SeekOrigin.Begin);

            LayerManager.Init(HeaderSettings.LayerCount);
            var layerDef = new LayerDef[LayerCount];


            progress.Reset(OperationProgress.StatusGatherLayers, HeaderSettings.LayerCount);
            uint[] layerDataAddresses = new uint[LayerCount];
            uint layerTableSize = 0;
            for (uint layerIndex = 0; layerIndex < HeaderSettings.LayerCount; layerIndex++)
            {
                progress.Token.ThrowIfCancellationRequested();
                layerDataAddresses[layerIndex] = inputFile.ReadUIntLittleEndian();

                layerDef[layerIndex] = Helpers.Deserialize<LayerDef>(inputFile);
                if (layerTableSize == 0)
                {
                    layerTableSize = 4 + (uint)Helpers.Serializer.SizeOf(layerDef[layerIndex]);
                }
                if (HeaderSettings.LayerTableSize > layerTableSize)
                {
                    inputFile.Seek(HeaderSettings.LayerTableSize - layerTableSize, SeekOrigin.Current);
                }

                progress++;
            }

            

            progress.Reset(OperationProgress.StatusDecodeLayers, HeaderSettings.LayerCount);
            var range = Enumerable.Range(0, (int)LayerCount);
            foreach (var batch in MoreEnumerable.Batch(range, Environment.ProcessorCount * 10))
            {
                var layerBytes = new byte[LayerCount][];

                foreach (var layerIndex in batch)
                {
                    progress.Token.ThrowIfCancellationRequested();

                    inputFile.Seek(layerDataAddresses[layerIndex], SeekOrigin.Begin);
                    layerBytes[layerIndex] = inputFile.ReadBytes(inputFile.ReadUIntLittleEndian());
                }

                Parallel.ForEach(batch, layerIndex =>
                {
                    if (progress.Token.IsCancellationRequested) return;
                    using var mat = DecodeImage(layerBytes[layerIndex], HeaderSettings.LayerDataType, Resolution);
                    var layer = new Layer((uint)layerIndex, mat, this);
                    layerDef[layerIndex].SetTo(layer);
                    this[layerIndex] = layer;
                    layerBytes[layerIndex] = null; // Clean

                    progress.LockAndIncrement();
                });
            }

            progress.Reset(OperationProgress.StatusDecodeGcode);
            inputFile.Seek(HeaderSettings.GCodeAddress, SeekOrigin.Begin);
            var gcodeDef = Helpers.Deserialize<GCodeDef>(inputFile);
            GCodeStr = gcodeDef.GCodeText;
            //GCode.ParseLayersFromGCode(this);

            UpdateGlobalPropertiesFromLayers();
        }

        public override void SaveAs(string filePath = null, OperationProgress progress = null)
        {
            if (RequireFullEncode)
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    FileFullPath = filePath;
                }
                Encode(FileFullPath, progress);
                return;
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                File.Copy(FileFullPath, filePath, true);
                FileFullPath = filePath;
            }

            using var outputFile = new FileStream(FileFullPath, FileMode.Open, FileAccess.Write);
            outputFile.Seek(0, SeekOrigin.Begin);
            FileSettings.Update();
            Helpers.SerializeWriteFileStream(outputFile, FileSettings);
            Helpers.SerializeWriteFileStream(outputFile, HeaderSettings);

            outputFile.Seek(HeaderSettings.LayerDefinitionsAddress, SeekOrigin.Begin);
            foreach (var layer in this)
            {
                outputFile.Seek(4, SeekOrigin.Current); // skip address
                Helpers.SerializeWriteFileStream(outputFile, new LayerDef(layer)); // Update layer values
            }

            if (HeaderSettings.GCodeAddress > 0)
            {
                outputFile.Seek(HeaderSettings.GCodeAddress, SeekOrigin.Begin);
                outputFile.SetLength(HeaderSettings.GCodeAddress);

                RebuildGCode();
                var gcodeSettings = new GCodeDef { GCodeText = GCodeStr };
                gcodeSettings.GCodeSize = (uint)gcodeSettings.GCodeText.Length;
                Helpers.SerializeWriteFileStream(outputFile, gcodeSettings);
            }
        }
        #endregion

        #region Static Methods

        public static byte[] EncodeImage(Mat mat, string encodeTo)
        {
            encodeTo = encodeTo.ToUpperInvariant();
            if (encodeTo is "PNG" or "JPG" or "JPEG" or "JP2" or "BMP" or "TIF" or "TIFF" or "PPM" or "PMG" or "SR" or "RAS")
            {
                return CvInvoke.Imencode($".{encodeTo.ToLowerInvariant()}", mat);
            }

            if (encodeTo is "RGB555" or "RGB565" or "RGB888"
                         or "BGR555" or "BGR565" or "BGR888")
            {
                var bytesPerPixel = encodeTo is "RGB888" or "BGR888" ? 3 : 2;
                var bytes = new byte[mat.Width * mat.Height * bytesPerPixel];
                uint index = 0;
                var span = mat.GetDataByteSpan();
                for (int i = 0; i < span.Length;)
                {
                    byte b = span[i++];
                    byte g;
                    byte r;

                    if (mat.NumberOfChannels == 1) // 8 bit safe-guard
                    {
                        r = g = b;
                    }
                    else
                    {
                        g = span[i++];
                        r = span[i++];
                    }

                    if (mat.NumberOfChannels == 4) i++; // skip alpha

                    switch (encodeTo)
                    {
                        case "RGB555":
                            var rgb555 = (ushort) (((r & 0b11111000) << 7) | ((g & 0b11111000) << 2) | (b >> 3));
                            BitExtensions.ToBytesLittleEndian(rgb555, bytes, index);
                            index += 2;
                            break;
                        case "RGB565":
                            var rgb565 = (ushort) (((r & 0b11111000) << 8) | ((g & 0b11111100) << 3) | (b >> 3));
                            BitExtensions.ToBytesLittleEndian(rgb565, bytes, index);
                            index += 2;
                            break;
                        case "RGB888":
                            bytes[index++] = r;
                            bytes[index++] = g;
                            bytes[index++] = b;
                            break;
                    }
                }

                return bytes;
            }

            throw new NotSupportedException($"The encode type: {encodeTo} is not supported.");
        }

        public static Mat DecodeImage(byte[] bytes, string decodeFrom, Size resolution)
        {
            if (decodeFrom is "PNG" or "JPG" or "JPEG" or "JP2" or "BMP" or "TIF" or "TIFF" or "PPM" or "PMG" or "SR" or "RAS")
            {
                var mat = new Mat();
                CvInvoke.Imdecode(bytes, ImreadModes.AnyColor, mat);
                return mat;
            }

            if (decodeFrom is "RGB555" or "RGB565" or "RGB888"
                           or "BGR555" or "BGR565" or "BGR888")
            {
                var mat = new Mat(resolution, DepthType.Cv8U, 3);
                var span = mat.GetDataByteSpan();
                var pixel = 0;
                for (int i = 0; i < bytes.Length;)
                {
                    switch (decodeFrom)
                    {
                        case "RGB555":
                            ushort rgb555 = BitExtensions.ToUShortLittleEndian(bytes, i);
                            span[pixel++] = (byte)((rgb555 & 0b00000000_00011111) << 3); // b
                            span[pixel++] = (byte)((rgb555 & 0b00000011_11100000) >> 2); // g
                            span[pixel++] = (byte)((rgb555 & 0b01111100_00000000) >> 7); // r
                            i += 2;
                            break;
                        case "RGB565":
                            ushort rgb565 = BitExtensions.ToUShortLittleEndian(bytes, i);
                            span[pixel++] = (byte)((rgb565 & 0b00000000_00011111) << 3); // b
                            span[pixel++] = (byte)((rgb565 & 0b00000111_11100000) >> 3); // g
                            span[pixel++] = (byte)((rgb565 & 0b11111000_00000000) >> 8); // r
                            i += 2;
                            break;
                        case "RGB888":
                            span[pixel++] = bytes[i + 2]; // b
                            span[pixel++] = bytes[i + 1]; // g
                            span[pixel++] = bytes[i];     // r
                            i += 3;
                            break;
                        case "BGR555":
                            ushort bgr555 = BitExtensions.ToUShortLittleEndian(bytes, i);
                            span[pixel++] = (byte)((bgr555 & 0b01111100_00000000) >> 7); // b
                            span[pixel++] = (byte)((bgr555 & 0b00000011_11100000) >> 2); // g
                            span[pixel++] = (byte)((bgr555 & 0b00000000_00011111) << 3); // r
                            i += 2;
                            break;
                        case "BGR565":
                            ushort bgr565 = BitExtensions.ToUShortLittleEndian(bytes, i);
                            span[pixel++] = (byte)((bgr565 & 0b11111000_00000000) >> 8); // b
                            span[pixel++] = (byte)((bgr565 & 0b00000111_11100000) >> 3); // g
                            span[pixel++] = (byte)((bgr565 & 0b00000000_00011111) << 3); // r
                            i += 2;
                            break;
                        case "BGR888":
                            span[pixel++] = bytes[i]; // b
                            span[pixel++] = bytes[i+1]; // g
                            span[pixel++] = bytes[i+2]; // r
                            i += 3;
                            break;
                    }
                }
                return mat;
            }

            throw new NotSupportedException($"The decode type: {decodeFrom} is not supported.");
        }

        public static Mat DecodeImage(byte[] bytes, string decodeFrom, uint resolutionX = 0, uint resolutionY = 0)
            => DecodeImage(bytes, decodeFrom, new Size((int) resolutionX, (int) resolutionY));


        #endregion
    }
}