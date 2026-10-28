using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

// =====================================================
//  Kết nối CSDL dùng chung
// =====================================================
public static class Db
{
    // Sửa Server cho đúng máy bạn:
    //   localhost | .\SQLEXPRESS | (localdb)\MSSQLLocalDB | TÊN-MÁY\SQLEXPRESS
    public const string ConnStr =
        @"Server=.\SQLEXPRESS;Database=ql_sinhvien;Integrated Security=True;TrustServerCertificate=True;";

    // Lấy dữ liệu hiển thị lên DataGridView
    // (chỉ truyền câu SQL cố định do lập trình viên viết, không nhận dữ liệu từ người dùng)
    public static async Task<DataTable> GetTableAsync(string sql)
    {
        var table = new DataTable();
        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var rd = await cmd.ExecuteReaderAsync();
        table.Load(rd);
        return table;
    }
}

// =====================================================
//  Đăng nhập
// =====================================================
public enum LoginResult { Success, WrongCredentials, Locked }

public static class AuthService
{
    private static byte[] ComputeHash(string salt, string password) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));

    public static async Task<(LoginResult Result, string? FullName)> LoginAsync(
        string username, string password)
    {
        await using var conn = new SqlConnection(Db.ConnStr);
        await conn.OpenAsync();

        const string sql =
            "SELECT PasswordHash, Salt, FullName, IsActive FROM Users WHERE Username = @u";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@u", SqlDbType.NVarChar, 50).Value = username;

        await using var rd = await cmd.ExecuteReaderAsync();
        if (!await rd.ReadAsync())
            return (LoginResult.WrongCredentials, null);

        string dbHash = rd.GetString(0);
        string salt = rd.GetString(1);
        string fullName = rd.IsDBNull(2) ? username : rd.GetString(2);
        bool isActive = rd.GetBoolean(3);

        bool ok = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(dbHash), ComputeHash(salt, password));

        if (!ok)
            return (LoginResult.WrongCredentials, null);

        return (isActive ? LoginResult.Success : LoginResult.Locked, fullName);
    }

    public static async Task WriteHistoryAsync(string username, bool success)
    {
        try
        {
            await using var conn = new SqlConnection(Db.ConnStr);
            await using var cmd = new SqlCommand(
                "INSERT INTO LoginHistory (Username, IsSuccess) VALUES (@u, @s)", conn);
            cmd.Parameters.Add("@u", SqlDbType.NVarChar, 50).Value = username;
            cmd.Parameters.Add("@s", SqlDbType.Bit).Value = success;
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Lỗi ghi log không được làm hỏng luồng đăng nhập
        }
    }
}

// =====================================================
//  Sinh viên (bảng SinhVien)
// =====================================================
public record SinhVien(
    string MaSV,
    string HoTen,
    DateTime NgaySinh,
    string GioiTinh,
    string Lop,
    decimal DiemTB);

public static class SinhVienService
{
    // Thêm sinh viên. Ném SqlException (Number 2627/2601) nếu trùng MaSV.
    public static async Task AddAsync(SinhVien sv)
    {
        const string sql =
            "INSERT INTO SinhVien (MaSV, HoTen, NgaySinh, GioiTinh, Lop, DiemTB) " +
            "VALUES (@ma, @ten, @ns, @gt, @lop, @diem)";

        await using var conn = new SqlConnection(Db.ConnStr);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@ma", SqlDbType.NVarChar, 20).Value = sv.MaSV;
        cmd.Parameters.Add("@ten", SqlDbType.NVarChar, 100).Value = sv.HoTen;
        cmd.Parameters.Add("@ns", SqlDbType.Date).Value = sv.NgaySinh;
        cmd.Parameters.Add("@gt", SqlDbType.NVarChar, 10).Value = sv.GioiTinh;
        cmd.Parameters.Add("@lop", SqlDbType.NVarChar, 50).Value = sv.Lop;

        var pDiem = cmd.Parameters.Add("@diem", SqlDbType.Decimal);
        pDiem.Precision = 4;
        pDiem.Scale = 2;
        pDiem.Value = sv.DiemTB;

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }
}

// =====================================================
//  Form chính: sinh viên + tài khoản + lịch sử đăng nhập
// =====================================================
public class frmMain : Form
{
    private const string SinhVienSql =
        "SELECT MaSV AS [Mã SV], HoTen AS [Họ tên], NgaySinh AS [Ngày sinh], " +
        "GioiTinh AS [Giới tính], Lop AS [Lớp], DiemTB AS [Điểm TB] " +
        "FROM SinhVien ORDER BY MaSV";

    private const string UsersSql =
        "SELECT Id AS [Mã], Username AS [Tên đăng nhập], FullName AS [Họ tên], " +
        "IsActive AS [Hoạt động], CreatedAt AS [Ngày tạo] FROM Users ORDER BY Id";

    private const string HistorySql =
        "SELECT TOP 200 Id AS [Mã], Username AS [Tên đăng nhập], " +
        "CASE WHEN IsSuccess = 1 THEN N'Thành công' ELSE N'Thất bại' END AS [Kết quả], " +
        "LoginTime AS [Thời gian] FROM LoginHistory ORDER BY LoginTime DESC";

    // --- Thanh trên cùng ---
    private readonly Label lblWelcome = new() { AutoSize = true, Location = new Point(12, 14), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
    private readonly Button btnRefresh = new() { Text = "Làm mới", Size = new Size(90, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
    private readonly Button btnLogout = new() { Text = "Đăng xuất", Size = new Size(90, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };

    // --- Form nhập sinh viên ---
    private readonly TextBox txtMaSV = new() { Location = new Point(95, 26), Size = new Size(170, 23), MaxLength = 20, TabIndex = 0 };
    private readonly TextBox txtHoTen = new() { Location = new Point(370, 26), Size = new Size(190, 23), MaxLength = 100, TabIndex = 1 };
    private readonly DateTimePicker dtpNgaySinh = new()
    {
        Location = new Point(95, 60),
        Size = new Size(170, 23),
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy",
        MinDate = new DateTime(1900, 1, 1),
        MaxDate = DateTime.Today,
        Value = new DateTime(2003, 1, 1),
        TabIndex = 2
    };
    private readonly ComboBox cboGioiTinh = new()
    {
        Location = new Point(370, 60),
        Size = new Size(190, 23),
        DropDownStyle = ComboBoxStyle.DropDownList,
        TabIndex = 3
    };
    private readonly TextBox txtLop = new() { Location = new Point(95, 94), Size = new Size(170, 23), MaxLength = 50, TabIndex = 4 };
    private readonly NumericUpDown nudDiemTB = new()
    {
        Location = new Point(370, 94),
        Size = new Size(190, 23),
        DecimalPlaces = 2,
        Minimum = 0,
        Maximum = 10,
        Increment = 0.1M,
        TabIndex = 5
    };
    private readonly Button btnThem = new() { Text = "Thêm sinh viên", Location = new Point(590, 26), Size = new Size(150, 32), TabIndex = 6 };
    private readonly Button btnNhapLai = new() { Text = "Nhập lại", Location = new Point(590, 64), Size = new Size(150, 32), CausesValidation = false, TabIndex = 7 };

    private readonly ErrorProvider errorProvider = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    // --- Bảng dữ liệu ---
    private readonly DataGridView dgvSinhVien = CreateGrid();
    private readonly DataGridView dgvUsers = CreateGrid();
    private readonly DataGridView dgvHistory = CreateGrid();

    // true nếu người dùng bấm "Đăng xuất" (để quay lại form đăng nhập)
    public bool LoggedOut { get; private set; }

    public frmMain(string fullName)
    {
        Text = "Quản lý sinh viên - ql_sinhvien (SQL Server)";
        ClientSize = new Size(780, 500);
        StartPosition = FormStartPosition.CenterScreen;

        errorProvider.ContainerControl = this;
        lblWelcome.Text = $"Xin chào, {fullName}";
        cboGioiTinh.Items.AddRange(new object[] { "Nam", "Nữ", "Khác" });
        cboGioiTinh.SelectedIndex = 0;

        // ----- Tab Sinh viên -----
        var grpSinhVien = new GroupBox { Text = "Thông tin sinh viên", Dock = DockStyle.Top, Height = 135 };
        grpSinhVien.Controls.AddRange(new Control[]
        {
            Lbl("Mã SV:", 12, 26),       txtMaSV,
            Lbl("Họ tên:", 287, 26),     txtHoTen,
            Lbl("Ngày sinh:", 12, 60),   dtpNgaySinh,
            Lbl("Giới tính:", 287, 60),  cboGioiTinh,
            Lbl("Lớp:", 12, 94),         txtLop,
            Lbl("Điểm TB:", 287, 94),    nudDiemTB,
            btnThem, btnNhapLai
        });

        var tabSinhVien = new TabPage("Sinh viên") { Padding = new Padding(3) };
        tabSinhVien.Controls.Add(dgvSinhVien);   // thêm lưới trước,
        tabSinhVien.Controls.Add(grpSinhVien);   // GroupBox Dock=Top sau để nằm trên cùng

        var tabUsers = new TabPage("Tài khoản") { Padding = new Padding(3) };
        tabUsers.Controls.Add(dgvUsers);

        var tabHistory = new TabPage("Lịch sử đăng nhập") { Padding = new Padding(3) };
        tabHistory.Controls.Add(dgvHistory);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.AddRange(new[] { tabSinhVien, tabUsers, tabHistory });

        // ----- Thanh trên -----
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 46, Width = ClientSize.Width };
        btnRefresh.Location = new Point(ClientSize.Width - 200, 9);
        btnLogout.Location = new Point(ClientSize.Width - 100, 9);
        topPanel.Controls.AddRange(new Control[] { lblWelcome, btnRefresh, btnLogout });

        Controls.Add(tabs);
        Controls.Add(topPanel);

        // ----- Sự kiện -----
        btnThem.Click += btnThem_Click;
        btnNhapLai.Click += (_, _) => ClearForm();
        btnRefresh.Click += async (_, _) => await LoadDataAsync();
        btnLogout.Click += (_, _) =>
        {
            LoggedOut = true;
            Close();
        };

        // Gõ lại thì xóa icon lỗi
        foreach (var c in new Control[] { txtMaSV, txtHoTen, txtLop })
            c.TextChanged += (_, _) => errorProvider.SetError(c, string.Empty);

        // Enter chỉ thêm sinh viên khi đang ở tab Sinh viên
        AcceptButton = btnThem;
        tabs.SelectedIndexChanged += (_, _) =>
            AcceptButton = tabs.SelectedTab == tabSinhVien ? btnThem : null;

        // Tải dữ liệu ngay khi form hiện ra
        Shown += async (_, _) => await LoadDataAsync();
    }

    private static Label Lbl(string text, int x, int y) =>
        new() { Text = text, Location = new Point(x, y + 3), AutoSize = true };

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window
    };

    // ----- Kiểm tra dữ liệu nhập -----
    private bool Check(Control c, bool hasError, string message)
    {
        errorProvider.SetError(c, hasError ? message : string.Empty);
        return !hasError;
    }

    private bool ValidateSinhVien()
    {
        bool ok = true;
        ok &= Check(txtMaSV, string.IsNullOrWhiteSpace(txtMaSV.Text), "Mã sinh viên không được để trống!");
        ok &= Check(txtHoTen, string.IsNullOrWhiteSpace(txtHoTen.Text), "Họ tên không được để trống!");
        ok &= Check(txtLop, string.IsNullOrWhiteSpace(txtLop.Text), "Lớp không được để trống!");
        return ok;
    }

    // ----- Thêm sinh viên -----
    private async void btnThem_Click(object? sender, EventArgs e)
    {
        if (!ValidateSinhVien())
        {
            // Đưa con trỏ về ô lỗi đầu tiên
            foreach (var c in new Control[] { txtMaSV, txtHoTen, txtLop })
            {
                if (errorProvider.GetError(c).Length > 0)
                {
                    c.Focus();
                    break;
                }
            }
            return;
        }

        var sv = new SinhVien(
            txtMaSV.Text.Trim().ToUpperInvariant(),
            txtHoTen.Text.Trim(),
            dtpNgaySinh.Value.Date,
            cboGioiTinh.Text,
            txtLop.Text.Trim(),
            nudDiemTB.Value);

        btnThem.Enabled = false;
        try
        {
            await SinhVienService.AddAsync(sv);

            MessageBox.Show($"Đã thêm sinh viên {sv.HoTen} ({sv.MaSV}).",
                "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

            ClearForm();
            await LoadDataAsync();
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            // Vi phạm PRIMARY KEY: trùng mã sinh viên
            errorProvider.SetError(txtMaSV, "Mã sinh viên đã tồn tại!");
            txtMaSV.Focus();
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"Không thêm được sinh viên:\n{ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnThem.Enabled = true;
        }
    }

    private void ClearForm()
    {
        txtMaSV.Clear();
        txtHoTen.Clear();
        txtLop.Clear();
        dtpNgaySinh.Value = new DateTime(2003, 1, 1);
        cboGioiTinh.SelectedIndex = 0;
        nudDiemTB.Value = 0;
        errorProvider.Clear();
        txtMaSV.Focus();
    }

    // ----- Tải dữ liệu từ CSDL -----
    private async Task LoadDataAsync()
    {
        btnRefresh.Enabled = false;
        try
        {
            dgvSinhVien.DataSource = await Db.GetTableAsync(SinhVienSql);
            dgvUsers.DataSource = await Db.GetTableAsync(UsersSql);
            dgvHistory.DataSource = await Db.GetTableAsync(HistorySql);

            SetFormat(dgvSinhVien, "Ngày sinh", "dd/MM/yyyy");
            SetFormat(dgvSinhVien, "Điểm TB", "0.00");
            SetFormat(dgvUsers, "Ngày tạo", "dd/MM/yyyy HH:mm");
            SetFormat(dgvHistory, "Thời gian", "dd/MM/yyyy HH:mm:ss");
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"Không tải được dữ liệu:\n{ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnRefresh.Enabled = true;
        }
    }

    private static void SetFormat(DataGridView grid, string column, string format)
    {
        if (grid.Columns[column] is { } col)
            col.DefaultCellStyle.Format = format;
    }
}

// =====================================================
//  Form đăng nhập
// =====================================================
public class frmLogin : Form
{
    private readonly Label lblUsername = new() { Text = "Tên đăng nhập:", Location = new Point(20, 25), AutoSize = true };
    private readonly Label lblPassword = new() { Text = "Mật khẩu:", Location = new Point(20, 70), AutoSize = true };

    private readonly TextBox txtUsername = new()
    {
        Name = "txtUsername",
        Location = new Point(130, 22),
        Size = new Size(190, 23),
        MaxLength = 50,
        TabIndex = 0
    };

    private readonly TextBox txtPassword = new()
    {
        Name = "txtPassword",
        Location = new Point(130, 67),
        Size = new Size(190, 23),
        UseSystemPasswordChar = true,
        TabIndex = 1
    };

    private readonly CheckBox chkShowPassword = new()
    {
        Name = "chkShowPassword",
        Text = "Hiện mật khẩu",
        Location = new Point(130, 100),
        AutoSize = true,
        TabIndex = 2
    };

    private readonly Button btnLogin = new()
    {
        Name = "btnLogin",
        Text = "Đăng nhập",
        Location = new Point(130, 140),
        Size = new Size(90, 32),
        TabIndex = 3
    };

    private readonly Button btnExit = new()
    {
        Name = "btnExit",
        Text = "Thoát",
        Location = new Point(230, 140),
        Size = new Size(90, 32),
        CausesValidation = false,
        TabIndex = 4
    };

    private readonly ErrorProvider errorProvider = new()
    {
        BlinkStyle = ErrorBlinkStyle.NeverBlink
    };

    public frmLogin()
    {
        Text = "Đăng nhập";
        ClientSize = new Size(360, 210);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        errorProvider.ContainerControl = this;

        Controls.AddRange(new Control[] { lblUsername, lblPassword, txtUsername, txtPassword,
                                          chkShowPassword, btnLogin, btnExit });

        chkShowPassword.CheckedChanged += (_, _) =>
            txtPassword.UseSystemPasswordChar = !chkShowPassword.Checked;

        btnLogin.Click += btnLogin_Click;
        btnExit.Click += (_, _) => Close();

        txtUsername.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(txtUsername.Text))
                errorProvider.SetError(txtUsername, string.Empty);
        };
        txtPassword.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(txtPassword.Text))
                errorProvider.SetError(txtPassword, string.Empty);
        };

        AcceptButton = btnLogin;   // Enter
        CancelButton = btnExit;    // Esc
    }

    private bool ValidateInput()
    {
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(txtUsername.Text))
        {
            errorProvider.SetError(txtUsername, "Tên đăng nhập không được để trống!");
            isValid = false;
        }
        else errorProvider.SetError(txtUsername, string.Empty);

        if (string.IsNullOrWhiteSpace(txtPassword.Text))
        {
            errorProvider.SetError(txtPassword, "Mật khẩu không được để trống!");
            isValid = false;
        }
        else errorProvider.SetError(txtPassword, string.Empty);

        return isValid;
    }

    private async void btnLogin_Click(object? sender, EventArgs e)
    {
        if (!ValidateInput())
        {
            if (errorProvider.GetError(txtUsername).Length > 0) txtUsername.Focus();
            else txtPassword.Focus();
            return;
        }

        string username = txtUsername.Text.Trim();
        string password = txtPassword.Text;

        btnLogin.Enabled = false;
        UseWaitCursor = true;

        try
        {
            var (result, fullName) = await AuthService.LoginAsync(username, password);

            switch (result)
            {
                case LoginResult.Success:
                    await AuthService.WriteHistoryAsync(username, true);
                    OpenMainForm(fullName ?? username);
                    break;

                case LoginResult.Locked:
                    await AuthService.WriteHistoryAsync(username, false);
                    MessageBox.Show("Tài khoản đã bị khóa!", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;

                default:
                    await AuthService.WriteHistoryAsync(username, false);
                    MessageBox.Show("Sai tên đăng nhập hoặc mật khẩu!", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtPassword.Clear();
                    txtPassword.Focus();
                    break;
            }
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"Không kết nối được CSDL:\n{ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            btnLogin.Enabled = true;
        }
    }

    // Ẩn form đăng nhập, mở form chính
    private void OpenMainForm(string fullName)
    {
        var main = new frmMain(fullName);

        main.FormClosed += (_, _) =>
        {
            if (main.LoggedOut)
            {
                // Đăng xuất -> quay lại form đăng nhập
                txtPassword.Clear();
                chkShowPassword.Checked = false;
                txtUsername.Focus();
                Show();
            }
            else
            {
                // Đóng form chính -> thoát chương trình
                Close();
            }
        };

        Hide();
        main.Show();
    }
}

// =====================================================
//  Điểm khởi chạy
// =====================================================
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new frmLogin());
    }
}
