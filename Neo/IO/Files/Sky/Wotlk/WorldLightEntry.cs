﻿using System;
using System.Collections.Generic;
using OpenTK;

namespace Neo.IO.Files.Sky.Wotlk
{
	internal class WorldLightEntry
    {
        private readonly IDataStorageRecord mLight;
        private readonly List<Vector3>[] mColorTables = new List<Vector3>[18];
        private readonly List<uint>[] mTimeTables = new List<uint>[18];

        private readonly List<float>[] mFloatTables = new List<float>[2];
        private readonly List<uint>[] mFloatTimes = new List<uint>[2];

        public bool IsGlobal { get; private set; }
        public float InnerRadius { get; private set; }
        public float OuterRadius { get; private set; }
        public Vector3 Position { get; private set; }

        public WorldLightEntry(IDataStorageRecord lightEntry)
        {
	        this.mLight = lightEntry;

            var px = this.mLight.GetFloat(2);
            var py = this.mLight.GetFloat(3);
            var pz = this.mLight.GetFloat(4);
            var ir = this.mLight.GetFloat(5);
            var or = this.mLight.GetFloat(6);

            px /= 36.0f;
            py /= 36.0f;
            pz /= 36.0f;
            ir /= 36.0f;
            or /= 36.0f;

	        this.IsGlobal = Math.Abs(px) < 1e-3 && Math.Abs(py) < 1e-3 && Math.Abs(pz) < 1e-3;
	        this.InnerRadius = ir;
	        this.OuterRadius = or;

	        this.Position = new Vector3(px, pz, py);

            for (var i = 0; i < 18; ++i)
            {
	            this.mColorTables[i] = new List<Vector3>();
	            this.mTimeTables[i] = new List<uint>();
            }

            for (var i = 0; i < 2; ++i)
            {
	            this.mFloatTables[i] = new List<float>();
	            this.mFloatTimes[i] = new List<uint>();
            }

            InitTables();
        }

        public float GetFloatForTime(LightFloat table, uint time)
        {
            int idx;
            switch (table)
            {
                case LightFloat.FogEnd:
                    idx = 0;
                    break;

                case LightFloat.FogScale:
                    idx = 1;
                    break;

                default:
                    return 1.0f;
            }

            if (idx < 0 || idx >= 2)
            {
	            return 0.0f;
            }

	        var timeValues = this.mFloatTimes[idx];
            var colorValues = this.mFloatTables[idx];
            if (timeValues.Count == 0)
            {
	            return 0.0f;
            }

	        time %= 2880;

            if (timeValues[0] > time)
            {
	            time = timeValues[0];
            }

	        if (timeValues.Count == 1)
	        {
		        return colorValues[0];
	        }

	        var v1 = 0.0f;
            var v2 = 0.0f;

            uint t1 = 0;
            uint t2 = 0;

            for (var i = 0; i < timeValues.Count; ++i)
            {
                if (i + 1 >= timeValues.Count)
                {
                    v1 = colorValues[i];
                    v2 = colorValues[0];
                    t1 = timeValues[i];
                    t2 = timeValues[0] + 2880;
                    break;
                }

                var ts = timeValues[i];
                var te = timeValues[i + 1];
                if (ts <= time && te >= time)
                {
                    t1 = ts;
                    t2 = te;
                    v1 = colorValues[i];
                    v2 = colorValues[i + 1];
                    break;
                }
            }

            var diff = t2 - t1;
            if (diff == 0)
            {
	            return v1;
            }

	        var sat = (time - t1) / (float)diff;
            return (1 - sat) * v1 + sat * v2;
        }

        public Vector3 GetColorForTime(LightColor table, uint time)
        {
            var idx = (int)table;
            time %= 2880;

            if (idx < 0 || idx >= 18)
            {
	            return Vector3.Zero;
            }

	        var timeValues = this.mTimeTables[idx];
            var colorValues = this.mColorTables[idx];
            if (timeValues.Count == 0)
            {
	            return Vector3.Zero;
            }

	        if (timeValues[0] > time)
	        {
		        time = timeValues[0];
	        }

	        if (timeValues.Count == 1)
	        {
		        return colorValues[0];
	        }

	        var v1 = Vector3.Zero;
            var v2 = Vector3.Zero;

            uint t1 = 0;
            uint t2 = 0;

            for (var i = 0; i < timeValues.Count; ++i)
            {
                if (i + 1 >= timeValues.Count)
                {
                    v1 = colorValues[i];
                    v2 = colorValues[0];
                    t1 = timeValues[i];
                    t2 = timeValues[0] + 2880;
                    break;
                }

                var ts = timeValues[i];
                var te = timeValues[i + 1];
                if (ts <= time && te >= time)
                {
                    t1 = ts;
                    t2 = te;
                    v1 = colorValues[i];
                    v2 = colorValues[i + 1];
                    break;
                }
            }

            var diff = t2 - t1;
            if (diff == 0)
            {
	            return v1;
            }

	        var sat = (time - t1) / (float)diff;
            return (1 - sat) * v1 + sat * v2;
        }

        private void InitTables()
        {
            var baseIndex = this.mLight.GetInt32(7) * 18;
            for (var i = 0; i < 18; ++i)
            {
                var lib = Storage.DbcStorage.LightIntBand.GetRowById(baseIndex + i - 17);
                if (lib == null)
                {
	                continue;
                }

	            var numEntries = lib.GetInt32(1);
                for (var j = 0; j < numEntries; ++j)
                {
	                this.mColorTables[i].Add(ToVector(lib.GetUint32(18 + j)));
	                this.mTimeTables[i].Add(lib.GetUint32(2 + j));
                }
            }

            baseIndex = this.mLight.GetInt32(7) * 6;
            for (var i = 0; i < 2; ++i)
            {
                var lfb = Storage.DbcStorage.LightFloatBand.GetRowById(baseIndex + i - 5);
                var numEntries = lfb.GetInt32(1);
                for (var j = 0; j < numEntries; ++j)
                {
	                this.mFloatTables[i].Add(lfb.GetFloat(18 + j));
	                this.mFloatTimes[i].Add(lfb.GetUint32(2 + j));
                }
            }
        }

        private static Vector3 ToVector(uint value)
        {
            return new Vector3(((value >> 16) & 0xFF) / 255.0f, ((value >> 8) & 0xFF) / 255.0f, ((value >> 0) & 0xFF) / 255.0f);
        }
    }
}