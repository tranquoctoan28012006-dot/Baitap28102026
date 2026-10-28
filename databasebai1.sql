

-- 1. Tạo CSDL ql_sinhvien
IF DB_ID('ql_sinhvien') IS NULL
    CREATE DATABASE ql_sinhvien;
GO

USE ql_sinhvien;
GO

-- 2. Tạo bảng SinhVien (dùng NVARCHAR để lưu được tiếng Việt)
IF OBJECT_ID('dbo.SinhVien') IS NULL
CREATE TABLE dbo.SinhVien
(
    MaSV     NVARCHAR(20)  NOT NULL PRIMARY KEY,
    HoTen    NVARCHAR(100) NOT NULL,
    NgaySinh DATE          NULL,
    GioiTinh NVARCHAR(10)  NULL,
    Lop      NVARCHAR(50)  NULL,
    DiemTB   DECIMAL(4,2)  NULL CHECK (DiemTB BETWEEN 0 AND 10)
);
GO

-- 3. Thêm dữ liệu mẫu (chạy lại nhiều lần cũng không bị trùng)
INSERT INTO dbo.SinhVien (MaSV, HoTen, NgaySinh, GioiTinh, Lop, DiemTB)
SELECT v.MaSV, v.HoTen, v.NgaySinh, v.GioiTinh, v.Lop, v.DiemTB
FROM (VALUES
    (N'SV001', N'Nguyễn Văn An',  CAST('2003-05-15' AS DATE), N'Nam', N'CNTT K18',    CAST(8.50 AS DECIMAL(4,2))),
    (N'SV002', N'Trần Thị Bình',  CAST('2003-08-20' AS DATE), N'Nữ',  N'CNTT K18',    CAST(9.20 AS DECIMAL(4,2))),
    (N'SV003', N'Lê Hoàng Cường', CAST('2002-11-10' AS DATE), N'Nam', N'Kế Toán K18', CAST(7.40 AS DECIMAL(4,2)))
) AS v (MaSV, HoTen, NgaySinh, GioiTinh, Lop, DiemTB)
WHERE NOT EXISTS (SELECT 1 FROM dbo.SinhVien s WHERE s.MaSV = v.MaSV);
GO

-- 4. Bảng tài khoản đăng nhập
IF OBJECT_ID('dbo.Users') IS NULL
CREATE TABLE dbo.Users
(
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash VARCHAR(64)   NOT NULL,          -- SHA-256 (hex) của Salt + Password
    Salt         VARCHAR(32)   NOT NULL,
    FullName     NVARCHAR(100) NULL,
    IsActive     BIT           NOT NULL DEFAULT 1,
    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- 5. Bảng lịch sử đăng nhập
IF OBJECT_ID('dbo.LoginHistory') IS NULL
CREATE TABLE dbo.LoginHistory
(
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    Username  NVARCHAR(50) NOT NULL,
    IsSuccess BIT          NOT NULL,
    LoginTime DATETIME     NOT NULL DEFAULT GETDATE()
);
GO

-- 6. Tài khoản mẫu: admin / 123  và  nguyenvana / abc123
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin')
    INSERT INTO dbo.Users (Username, PasswordHash, Salt, FullName)
    VALUES (N'admin',
            CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'a1b2c3d4e5f6' + '123'), 2),
            'a1b2c3d4e5f6',
            N'Quản trị viên');

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'nguyenvana')
    INSERT INTO dbo.Users (Username, PasswordHash, Salt, FullName)
    VALUES (N'nguyenvana',
            CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'f6e5d4c3b2a1' + 'abc123'), 2),
            'f6e5d4c3b2a1',
            N'Nguyễn Văn A');
GO

-- Kiểm tra
SELECT * FROM dbo.SinhVien;
SELECT Id, Username, FullName, IsActive FROM dbo.Users;
USE ql_sinhvien;
SELECT * FROM SinhVien;