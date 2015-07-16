using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using YALV.Core.Domain;
using YALV.Properties;

namespace YALV.Common
{
    public class AllFilesItem : FileItem
    {
        private readonly ICollection<FileItem> _otherFiles;

        public AllFilesItem(ICollection<FileItem> otherFiles)
            : base(Resources.FileList_AllFiles, Resources.FileList_AllFiles_Description)
        {
            _otherFiles = otherFiles;
            foreach (var otherFile in otherFiles)
            {
                otherFile.PropertyChanged += OtherFileOnPropertyChanged;
            }
        }

        private void OtherFileOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == "Checked")
            {
                var fileItem = (FileItem)sender;
                if (!fileItem.Checked)
                {
                    base.Checked = false;
                }
            }
        }

        public override bool Checked
        {
            get { return base.Checked && _otherFiles.All(f => f.Checked); }
            set
            {
                base.Checked = value;
                if (_otherFiles != null)
                {
                    foreach (var file in _otherFiles)
                    {
                        file.Checked = value;
                    }
                }
            }
        }
    }
}