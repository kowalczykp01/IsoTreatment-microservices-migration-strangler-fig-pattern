using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TreatmentService.Infrastructure.Persistence.Configurations;

internal sealed class UserRowConfiguration : IEntityTypeConfiguration<UserRow>
{
    public void Configure(EntityTypeBuilder<UserRow> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .ValueGeneratedOnAdd();
    }
}
