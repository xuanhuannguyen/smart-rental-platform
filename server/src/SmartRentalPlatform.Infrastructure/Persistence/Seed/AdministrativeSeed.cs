using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed
{
    public static class AdministrativeSeed
    {
        private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static AdministrativeProvince[] GetProvinces()
        {
            return
            [
                new AdministrativeProvince
            {
                Code = "HN",
                Name = "Ha Noi",
                Type = ProvinceType.City,
                IsActive = true,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new AdministrativeProvince
            {
                Code = "HCM",
                Name = "Ho Chi Minh",
                Type = ProvinceType.City,
                IsActive = true,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            }
            ];
        }

        public static AdministrativeDistrict[] GetDistricts()
        {
            return
            [
                new AdministrativeDistrict
            {
                Code = "HN-CG",
                ProvinceCode = "HN",
                Name = "Cau Giay",
                Type = DistrictType.District,
                IsActive = true,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new AdministrativeDistrict
            {
                Code = "HCM-Q1",
                ProvinceCode = "HCM",
                Name = "District 1",
                Type = DistrictType.District,
                IsActive = true,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            }
            ];
        }

        public static AdministrativeWard[] GetWards()
        {
            return
            [
                new AdministrativeWard
            {
                Code = "HN-CG-DV",
                DistrictCode = "HN-CG",
                Name = "Dich Vong",
                Type = WardType.Ward,
                IsActive = true,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new AdministrativeWard
            {
                Code = "HCM-Q1-BN",
                DistrictCode = "HCM-Q1",
                Name = "Ben Nghe",
                Type = WardType.Ward,
                IsActive = true,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            }
            ];
        }
    }
}
