﻿using Nop.Web.Framework.Models;

namespace Nop.Web.Models.Api.Catalog
{
    /// <summary>
    /// Represents a product specification attribute value model
    /// </summary>
    public record ProductSpecificationAttributeValueApiModel : BaseNopModel
    {
        #region Properties

        /// <summary>
        /// Gets or sets the attribute type id
        /// </summary>
        public int AttributeTypeId { get; set; }

        public bool AllowFiltering { get; set; }

        /// <summary>
        /// Gets or sets the value raw. This value is already HTML encoded
        /// </summary>
        public string ValueRaw { get; set; }

        /// <summary>
        /// Gets or sets the option color (if specified). Used to display color squares
        /// </summary>
        public string ColorSquaresRgb { get; set; }

        #endregion
    }
}
