using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chaos
{

    public static class UnsafeUtil
    {
        public static float UintToFloat(uint u)
        {
            unsafe
            {
                return *(float*)&u;
            }
        }

        public static uint FloatToUint(float f)
        {
            unsafe
            {
                return *(uint*) &f;
            }
        }
    }


}