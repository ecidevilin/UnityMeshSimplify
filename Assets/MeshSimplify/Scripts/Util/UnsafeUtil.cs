using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

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
		[StructLayout( LayoutKind.Sequential )]
		private struct ArrayHeader {
			internal IntPtr type;
			internal int length;
		}

		public unsafe static void IntegerHackArraySize( int[] array, int size) 
		{
			if ( array != null ) {
				if ( size < array.Length ) {
					fixed ( void* p = array ) {
						ArrayHeader* header = ( ( ArrayHeader* )p ) - 1;
						header->length = size;
					}
				}
			}
		}
		public unsafe static void Vector2HackArraySize( Vector2[] array, int size)
		{
			if ( array != null ) {
				if ( size < array.Length ) {
					fixed ( void* p = array ) {
						ArrayHeader* header = ( ( ArrayHeader* )p ) - 1;
						header->length = size;
					}
				}
			}
		}
		public unsafe static void Vector3HackArraySize( Vector3[] array, int size)
		{
			if ( array != null ) {
				if ( size < array.Length ) {
					fixed ( void* p = array ) {
						ArrayHeader* header = ( ( ArrayHeader* )p ) - 1;
						header->length = size;
					}
				}
			}
		}
		public unsafe static void Vector4HackArraySize( Vector4[] array, int size)
		{
			if ( array != null ) {
				if ( size < array.Length ) {
					fixed ( void* p = array ) {
						ArrayHeader* header = ( ( ArrayHeader* )p ) - 1;
						header->length = size;
					}
				}
			}
		}
		public unsafe static void Color32HackArraySize( Color32[] array, int size)
		{
			if ( array != null ) {
				if ( size < array.Length ) {
					fixed ( void* p = array ) {
						ArrayHeader* header = ( ( ArrayHeader* )p ) - 1;
						header->length = size;
					}
				}
			}
		}
		public unsafe static void BoneWeightHackArraySize( BoneWeight[] array, int size)
		{
			if ( array != null ) {
				if ( size < array.Length ) {
					fixed ( void* p = array ) {
						ArrayHeader* header = ( ( ArrayHeader* )p ) - 1;
						header->length = size;
					}
				}
			}
		}
    }

}