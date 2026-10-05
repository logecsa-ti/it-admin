using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TIAdmin.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migracion de datos (Q-10 / ADR-020): el rol USER deja de ver el inventario completo.
    /// El seed solo aplica la matriz al crear un rol (ADR-017), asi que las bases existentes
    /// necesitan este ajuste explicito. Las bases nuevas ya nacen sin el permiso.
    /// </summary>
    public partial class RemoveAssetsViewFromUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rp
                FROM RolePermissions rp
                INNER JOIN Roles r ON r.Id = rp.RoleId
                INNER JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE r.NormalizedName = 'USER' AND p.Code = 'ASSETS.VIEW';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id
                FROM Roles r
                CROSS JOIN Permissions p
                WHERE r.NormalizedName = 'USER' AND p.Code = 'ASSETS.VIEW'
                  AND NOT EXISTS (
                      SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
                """);
        }
    }
}
