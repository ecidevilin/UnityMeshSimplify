using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UltimateGameTools
{
	namespace MeshSimplifier
	{
		public struct Vector3Int
		{
			private int _x;
			private int _y;
			private int _z;
			public Vector3Int(int x, int y, int z)
			{
				_x = x;
				_y = y;
				_z = z;
			}
			public int this[int idx]
			{
				get { 
					switch (idx) {
					case 0:
						return _x;
						break;
					case 1:
						return _y;
						break;
					case 2:
						return _z;
						break;
					default:
						return 0;
					}
				}
				set { 
					switch (idx) {
					case 0:
						_x = value;
						break;
					case 1:
						_y = value;
						break;
					case 2:
						_z = value;
						break;
					}
				}
			}
		}
	}
}
