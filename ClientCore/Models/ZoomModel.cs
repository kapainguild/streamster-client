using DynamicStreamer;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class ZoomModel
    {
        private const double MoveStepH = 0.0125;
        private const double MoveStepV = 0.025;

        private double _x;
        private double _y;

        public Property<double> Zoom { get; } = new Property<double>(1.0);

        public Property<bool> HasZoom { get; } = new Property<bool>(false);

        public Action Changed { get; set; }


        public Action Set1x { get; }

        public Action Set2x { get; }

        public Action Set3x { get; }

        public Action Left { get; } 

        public Action Right { get; } 

        public Action Up { get; } 

        public Action Down { get; }

        public Property<bool> CanLeft { get; } = new Property<bool>();

        public Property<bool> CanRight { get; } = new Property<bool>();

        public Property<bool> CanUp { get; } = new Property<bool>();

        public Property<bool> CanDown { get; } = new Property<bool>();

        public List<SettingsSelectorData<ZoomResolutionBehavior>> ZoomBehaviors { get; } = new List<SettingsSelectorData<ZoomResolutionBehavior>>
        {
            new SettingsSelectorData<ZoomResolutionBehavior> { Value = ZoomResolutionBehavior.Never, DisplayName = "Never" },
            new SettingsSelectorData<ZoomResolutionBehavior> { Value = ZoomResolutionBehavior.DependingOnZoom, DisplayName = "Based on zoom" },
            new SettingsSelectorData<ZoomResolutionBehavior> { Value = ZoomResolutionBehavior.Always, DisplayName = "Always maximum" },
        };

        public Property<SettingsSelectorData<ZoomResolutionBehavior>> ZoomBehavior { get; } = new Property<SettingsSelectorData<ZoomResolutionBehavior>>();

        public Property<bool> ZoomBehaviorEnabled { get; } = new Property<bool>();

        public ZoomModel()
        {
            Set1x = () => Zoom.Value = 1.0;
            Set2x = () => Zoom.Value = 2.0;
            Set3x = () => Zoom.Value = 3.0;

            Left = () => MoveTo(_x - MoveStepH / Zoom.Value, _y);
            Right = () => MoveTo(_x + MoveStepH / Zoom.Value, _y);

            Up = () => MoveTo(_x, _y - MoveStepV / Zoom.Value);
            Down = () => MoveTo(_x, _y + MoveStepV / Zoom.Value);

            Zoom.OnChange = (a, b) => DoZoom(a, b, 0.5, 0.5);
            ZoomBehavior.OnChange = (o, n) => Changed();
        }

        private void MoveTo(double x, double y)
        {
            _x = x;
            _y = y;
            AdjustRect();
            UpdateHasZoom();
            Changed();
        }

        private void UpdateHasZoom()
        {
            HasZoom.Value = Zoom.Value != 1.0;

            CanLeft.Value = Zoom.Value != 1.0 && _x > 0;
            CanRight.Value = Zoom.Value != 1.0 && _x + GetExtent() < 1.0;

            CanUp.Value = Zoom.Value != 1.0 && _y > 0;
            CanDown.Value = Zoom.Value != 1.0 && _y + GetExtent() < 1.0;
        }

        public void MoveDelta((double x, double y) dragStart, (double x, double y) current, SceneRect dragStartModel)
        {
            var extent = GetExtent();
            MoveTo(dragStartModel.L - (current.x - dragStart.x) * extent, dragStartModel.T - (current.y - dragStart.y) * extent);
        }

        public void DoZoom(double oldZoom, double newZoom, double xCenter, double yCenter)
        {
            if (newZoom < 1)
                newZoom = 1;
            if (newZoom > 3)
                newZoom = 3;

            Zoom.SilentValue = newZoom;

            var oldExtent = 1 / oldZoom;
            var newExtent = 1 / newZoom;

            _x = _x + oldExtent * xCenter - newExtent * xCenter;
            _y = _y + oldExtent * yCenter - newExtent * yCenter;

            AdjustRect();
            UpdateHasZoom();
            Changed();
        }

        private double GetExtent() => 1 / Zoom.Value;

        private void AdjustRect()
        {
            var extent = GetExtent();

            if (_x < 0.0)
                _x = 0.0;

            if (_x > 1.0 - extent)
                _x = 1.0 - extent;

            if (_y < 0.0)
                _y = 0.0;

            if (_y > 1.0 - extent)
                _y = 1.0 - extent;
        }

        public SceneRect GetPtz()
        {
            return new SceneRect(_x, _y, GetExtent(), GetExtent());
        }

        public void SetPtz(SceneRect rect, ZoomResolutionBehavior zoomResolution, bool zoomResolutionEnabled)
        {
            _x = rect.L;
            _y = rect.T;
            Zoom.SilentValue = 1 / rect.W;
            ZoomBehavior.SilentValue = ZoomBehaviors.FirstOrDefault(s => s.Value == zoomResolution);
            ZoomBehaviorEnabled.Value = zoomResolutionEnabled;
            UpdateHasZoom();
        }
    }

}
