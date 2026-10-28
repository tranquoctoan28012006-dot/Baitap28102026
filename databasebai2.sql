

IF DB_ID('ql_sinhvien') IS NULL
    CREATE DATABASE ql_sinhvien;
GO

USE ql_sinhvien;
GO

-- 1. Bảng khóa học (nguồn dữ liệu cho ComboBox)
IF OBJECT_ID('dbo.KhoaHoc') IS NULL
CREATE TABLE dbo.KhoaHoc
(
    MaKH   INT           NOT NULL PRIMARY KEY,     -- ValueMember
    TenKH  NVARCHAR(100) NOT NULL,                 -- DisplayMember
    HocPhi DECIMAL(12,0) NOT NULL DEFAULT 0
);
GO

-- 2. Bảng đăng ký
IF OBJECT_ID('dbo.DangKy') IS NULL
CREATE TABLE dbo.DangKy
(
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    HoTen       NVARCHAR(100) NOT NULL,
    GioiTinh    NVARCHAR(10)  NOT NULL,
    NgaySinh    DATE          NOT NULL,
    SoDienThoai VARCHAR(20)   NOT NULL,
    MaKH        INT           NOT NULL,
    TuyChon     NVARCHAR(200) NULL,                -- các CheckBox đã tick, cách nhau bằng dấu phẩy
    NgayDangKy  DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_DangKy_KhoaHoc FOREIGN KEY (MaKH) REFERENCES dbo.KhoaHoc(MaKH)
);
GO

-- 3. Dữ liệu mẫu cho khóa học (chạy lại nhiều lần không bị trùng)
INSERT INTO dbo.KhoaHoc (MaKH, TenKH, HocPhi)
SELECT v.MaKH, v.TenKH, v.HocPhi
FROM (VALUES
    (1, N'Lập trình C# căn bản',        CAST(3500000 AS DECIMAL(12,0))),
    (2, N'Lập trình Web ASP.NET',       CAST(4000000 AS DECIMAL(12,0))),
    (3, N'Cơ sở dữ liệu SQL Server',    CAST(3000000 AS DECIMAL(12,0))),
    (4, N'Tiếng Anh giao tiếp',         CAST(2500000 AS DECIMAL(12,0))),
    (5, N'Thiết kế đồ họa',             CAST(3200000 AS DECIMAL(12,0)))
) AS v (MaKH, TenKH, HocPhi)
WHERE NOT EXISTS (SELECT 1 FROM dbo.KhoaHoc k WHERE k.MaKH = v.MaKH);
GO

-- Kiểm tra
SELECT * FROM dbo.KhoaHoc;
SELECT * FROM dbo.DangKy;
