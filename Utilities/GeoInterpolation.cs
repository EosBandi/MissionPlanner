using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MissionPlanner.Utilities
{


    public class GeoInterpolation
    {
        private const double EarthRadiusMeters = 6371000; // Earth's radius in meters
        private const double MetersPerDegreeLatitude = 111320; // Approximate meters per degree of latitude

        /// <summary>
        /// Calculates points at n-meter intervals between two geographical coordinates
        /// using a simple linear interpolation (suitable for shorter distances)
        /// </summary>
        /// <param name="startLat">Starting point latitude in degrees</param>
        /// <param name="startLon">Starting point longitude in degrees</param>
        /// <param name="endLat">Ending point latitude in degrees</param>
        /// <param name="endLon">Ending point longitude in degrees</param>
        /// <param name="alt">The Terrain altitude for the segment, assuming the begining and the end is the same</param>
        /// <param name="resolution"> intervals to split the line</param>
        /// <returns>List of coordinates (latitude, longitude pairs) at 4-meter intervals</returns>
        public static List<(double Latitude, double Longitude, double alt)> GetPointsAtInterval(
            double startLat, double startLon, double endLat, double endLon, double alt, double resolution)
        {
            // Calculate total distance between points in meters using Haversine formula
            double totalDistance = GetDistanceInMeters(startLat, startLon, endLat, endLon);

            // Calculate number of points needed at n-meter intervals
            int numPoints = (int)Math.Ceiling(totalDistance / resolution);

            // Initialize the result list with the starting point
            var result = new List<(double Latitude, double Longitude, double alt)>
        {
            (startLat, startLon, alt)
        };

            // If points are identical or very close, just return the start point
            if (numPoints <= 1)
            {
                return result;
            }
            // Calculate step sizes for latitude and longitude in degrees
            double latStep = (endLat - startLat) / numPoints;
            double lonStep = (endLon - startLon) / numPoints;

            // Generate intermediate points using linear interpolation
            for (int i = 1; i < numPoints; i++)
            {
                double lat = startLat + latStep * i;
                double lon = startLon + lonStep * i;
                result.Add((lat, lon, alt));
            }

            // Add the end point
            result.Add((endLat, endLon, alt));

            return result;
        }

        /// <summary>
        /// Calculates the distance between two points on Earth in meters
        /// </summary>
        private static double GetDistanceInMeters(double lat1, double lon1, double lat2, double lon2)
        {
            // Convert degrees to radians
            double lat1Rad = ToRadians(lat1);
            double lon1Rad = ToRadians(lon1);
            double lat2Rad = ToRadians(lat2);
            double lon2Rad = ToRadians(lon2);

            // Simple Haversine formula for distance calculation
            double dLat = lat2Rad - lat1Rad;
            double dLon = lon2Rad - lon1Rad;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusMeters * c;
        }

        /// <summary>
        /// Converts degrees to radians
        /// </summary>
        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }

    // Example usage:
    // var points = GeoInterpolation.GetPointsAtInterval(47.6062, -122.3321, 47.6097, -122.3331);
    // foreach (var point in points)
    // {
    //     Console.WriteLine($"Latitude: {point.Latitude}, Longitude: {point.Longitude}");
    // }
}
