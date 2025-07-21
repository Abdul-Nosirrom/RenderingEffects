#pragma once

// result = b * t + a * (1 - t) = a + b * t - a * t
// t = ( result - b ) / ( a - b )
float InverseLerp(float a, float b, float result)
{
	return (result - a) / (b - a);
}