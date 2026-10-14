using System.ComponentModel.DataAnnotations;

namespace ProjectIntern.Web.ViewModels.Admin.Topic;

public class TopicEditInputModel
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public Guid specialityId { get; set; }
    [Required]
    [MinLength(4)]
    [MaxLength(150)]
    public string Name { get; set; } = null!;
    [Required]
    [MinLength(8)]
    public string Description { get; set; } = null!;
}
