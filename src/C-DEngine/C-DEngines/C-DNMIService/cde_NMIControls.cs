// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.Engines.NMIService
{

    public class nmiPlatBag :TheNMIBaseControl
    {
        /// <summary>
        /// Node side: Does not send the control to the client/browser
        /// </summary>
        public bool? Hide { get; set; }
        /// <summary>
        /// Node side: Does not send the control to the client/browser
        /// </summary>
        public bool? Show { get; set; }

        /// <summary>
        /// Node side: Does not send the control to client/browser if target view is a table
        /// </summary>
        public bool? HideInTable { get; set; }
        /// <summary>
        /// Node side: Does not send the control to client/browser if target view is a table
        /// </summary>
        public bool? ShowInTable { get; set; }

        /// <summary>
        /// Node side: does not send the control to client/browser if target view is a form
        /// </summary>
        public bool? HideInForm { get; set; }
        /// <summary>
        /// Node side: does not send the control to client/browser if target view is a form
        /// </summary>
        public bool? ShowInForm { get; set; }

        /// <summary>
        /// Control only shows if requested from First Node
        /// </summary>
        public bool? AllowAllNodes { get; set; }
        /// <summary>
        /// Only show if on First Node
        /// </summary>
        public bool? RequireFirstNode { get; set; }

        /// <summary>
        /// Do not allow input to the Control
        /// </summary>
        public bool? ReadOnly { get; set; }
        /// <summary>
        /// Do allow input to the Control
        /// </summary>
        public bool? ReadWrite { get; set; }

        /// <summary>
        /// Does not show the Add Button in Tables
        /// </summary>
        public bool? HideAdd { get; set; }
        /// <summary>
        /// Does not show the Add Button in Tables
        /// </summary>
        public bool? ShowAdd { get; set; }
        /// <summary>
        /// Control only shows if requested from First Node
        /// </summary>
        public bool? AllowAddOnAllNodes { get; set; }
        /// <summary>
        /// Only show if on First Node
        /// </summary>
        public bool? RequireFirstNodeForAdd { get; set; }
    }


    /// <summary>
    /// Propertybag generator for NMI Forms
    /// </summary>
    public class nmiCtrlFormView : TheNMIBaseControl
    {
        /// <summary>
        /// sets the background color
        /// </summary>
        public string Background { get; set; }
        /// <summary>
        /// Hides the caption of a form
        /// </summary>
        public bool? HideCaption { get; set; }

        /// <summary>
        /// Sets a custom name for the new Sidebar
        /// </summary>
        public string SideBarTitle { get; set; }

        /// <summary>
        /// Sets a valid Font-Awesome icon for the sidebar
        /// </summary>
        public string SideBarIconFA { get; set; }

        /// <summary>
        /// Set a start Group for form start
        /// </summary>
        public string StartGroup { get; set; }

        /// <summary>
        /// Sets the Template FormID to a table. You can than use the normal "DETAILS" button to switch to the template
        /// </summary>
        public string TemplateID { get; set; }

        /// <summary>
        /// Adds Margin to all groups in the form
        /// </summary>
        public bool? UseMargin { get; set; }
    }

    public class nmiCtrlFormTemplate : nmiCtrlFormView
    {
        /// <summary>
        /// References the Table to connect to
        /// </summary>
        public string TableReference { get; set; }

        public Guid? CancelScreenID { get; set; }
        public Guid? FinishScreenID { get; set; }

        public bool? IsPopup { get; set; }
    }

    public class nmiCtrlWizard : nmiCtrlFormTemplate
    {
        public nmiCtrlWizard()
        {
            StartGroup = "WizarePage:1";
        }

        public string TileThumbnail { get; set; }

        public string PanelTitle { get; set; }
        public string FormTitle { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }

        public int? FldOrder { get; set; }
        public int? Flags { get; set; }

        public bool? AllowReuse { get; set; }

        public bool? HideFromSideBar { get; set; }
    }

    /// <summary>
    /// Propertybag generator for NMI Forms
    /// </summary>
    public class nmiCtrlIFrameView : TheNMIBaseControl
    {

        /// <summary>
        /// Event fired if the Iframe was loaded
        /// </summary>
        public string OnIFrameLoaded { get; set; }
        /// <summary>
        /// The URL of the IFrame content
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Adds a header to the request in the source. syntax: Name=value
        /// </summary>
        public string AddHeader { get; set; }

        /// <summary>
        /// sets the background color
        /// </summary>
        public string Background { get; set; }
        /// <summary>
        /// Hides the caption of a form
        /// </summary>
        public bool? HideCaption { get; set; }
    }


    /// <summary>
    /// All NMI Controls are derived from TheNMIBaseControl
    /// </summary>
    public class TheNMIBaseControl
    {
        public Guid? MID { get; set; }
        /// <summary>
        /// The meaning of "Value" is unique to each control.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets a custom property. This can be used for controls that have private extensions
        /// </summary>
        /// <value>The custom.</value>
        public string Custom { get; set; }

        /// <summary>
        /// Allows to set a custom cookie on a control
        /// </summary>
        public string Cookie { get; set; }
        /// <summary>
        /// Sets the visibilty of the control.
        /// </summary>
        public bool? Visibility { get; set; }

        /// <summary>
        /// Groups visibility of multiple objects. Only one is visible at a time. (i.e. Tab Controls)
        /// Syntax: "GroupName:ID"
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Makes the control disabled.
        /// </summary>
        public bool? Disabled { get; set; }

        /// <summary>
        /// Sets the width of the control. (1 tile = 78px)
        /// </summary>
        public int TileWidth { get; set; }

        /// <summary>
        /// Control will always size as its parent
        /// </summary>
        public bool? InheritWidth { get; set; }
        /// <summary>
        /// Creates smaller tiles (2 = 1/2 Tiles, 4=1/4 Tiles) Do not use other values then 2 or 4 unless you know exactly what you are doing. Only exponents of 2 are working in our grid
        /// </summary>
        public int TileFactorX { get; set; }
        /// <summary>
        /// Creates smaller tiles (2 = 1/2 Tiles, 4=1/4 Tiles) Do not use other values then 2 or 4 unless you know exactly what you are doing. Only exponents of 2 are working in our grid
        /// </summary>
        public int TileFactorY { get; set; }

        /// <summary>
        /// Sets the height of the control. (1 tile = 78px)
        /// </summary>
        public int TileHeight { get; set; }

        /// <summary>
        /// Sets the depth of the control (Z Axes)
        /// </summary>
        public int TileDepth { get; set; }
        /// <summary>
        /// Sets the max width of the control.
        /// </summary>
        public int MaxTileWidth { get; set; }

        /// <summary>
        /// sets the minimum width of the control
        /// </summary>
        public int MinTileWidth { get; set; }
        /// <summary>
        /// Sets the Class name used in a style sheet
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Adds a classname to the classlist of the control.
        /// </summary>
        public string AddClassName { get; set; }

        /// <summary>
        /// Removes a classname from the classlist of a control
        /// </summary>
        public string RemoveClassName { get; set; }
        /// <summary>
        /// Sets the Class name for the sourounding TileEntry (TE) of an element
        /// </summary>
        public string TEClassName { get; set; }

        /// <summary>
        /// Can use to set any CSS style
        /// </summary>
        public string Style { get; set; }

        /// <summary>
        /// Gets the control type. Only for custom/User controls
        /// </summary>
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        public virtual string ControlType { get; }

        /// <summary>
        /// Tells the NMI Engine what Plugins the control is in
        /// </summary>
        public virtual string EngineName
        {
            get { return mEngineName; }
            set { mEngineName = value; }
        }
        private string mEngineName = "NMIService";

        /// <summary>
        /// Absolute Left Position in Tile Units
        /// </summary>
        public int TileLeft { get; set; }

        /// <summary>
        /// Absolute Top Position in Tile Units
        /// </summary>
        public int TileTop { get; set; }

        /// <summary>
        /// Absolute Depth (Z) position in tile units
        /// </summary>
        public int TileBack { get; set; }
        /// <summary>
        /// Absolute Top position in Pixels
        /// </summary>
        public int Top { get; set; }

        /// <summary>
        /// Absolute left Position in Pixels
        /// </summary>
        public int Left { get; set; }

        /// <summary>
        /// Absolute Z Position in Pixels
        /// </summary>
        public int Back { get; set; }

        /// <summary>
        /// Sets the small text below the control
        /// </summary>
        public string HelpText { get; set; }

        /// <summary>
        /// A short text that is shown when a user hovers over an item
        /// </summary>
        public string ToolTip { get; set; }

        /// <summary>
        /// Sets the width of control in pixels
        /// </summary>
        public int PixelWidth { get; set; }

        /// <summary>
        /// Sets the height of control in pixels
        /// </summary>
        public int PixelHeight { get; set; }

        /// <summary>
        /// Sets the depth of the control in pixels (Z-Axes)
        /// </summary>
        public int PixelDepth { get; set; }

        /// <summary>
        /// Prevents Multitouch on the control
        /// </summary>
        public bool? PreventManipulation { get; set; }

        /// <summary>
        /// Prevents routing of event to parent controls
        /// </summary>
        public bool? PreventDefault { get; set; }


        /// <summary>
        /// Enables Multitouch on the control
        /// </summary>
        public bool? EnableMT { get; set; }

        /// <summary>
        /// Registers a script for the OnValueChange event of the control (JavaScript)
        /// </summary>
        public string OnValueChanged { get; set; }

        /// <summary>
        /// Registers a script for the OniValueChange event of the control, (JavaScript)
        /// </summary>
        public string OniValueChanged { get; set; }

        /// <summary>
        /// Registers a script for a custom event of the control, (javascript)
        /// </summary>
        public string RegisterEvent { get; set; }

        /// <summary>
        /// Registers a script for the "Property Changed" event of the control, (JavaScript)
        /// </summary>
        public string OnPropertyChanged { get; set; }

        /// <summary>
        /// Registers a script for OnPropertySetChange event of the control, (JavaScript)
        /// </summary>
        public string OnPropertySet { get; set; }

        /// <summary>
        /// Sets the opacity of the control
        /// </summary>
        public double Opacity { get; set; }

        /// <summary>
        /// defines the Float of the control (Left, Right, none)
        /// </summary>
        public string Float { get; set; }

        /// <summary>
        /// Aligns text horizontally in the control according to the setting inside the Control Section of the TileEntry. Allowed is 'left', 'right', 'center'
        /// </summary>
        public string HorizontalAlignment { get; set; }

        /// <summary>
        /// Aligns text vertically in the control according to the setting inside the Control Section of the TileEntry. Allowed is 'top', 'bottom', 'center'
        /// </summary>
        public string VerticalAlignment { get; set; }
        /// <summary>
        /// The Font Size in Pixels
        /// </summary>
        public int FontSize { get; set; }

        /// <summary>
        /// Sets the ZIndex of an HTML Element in the browser
        /// </summary>
        public int ZIndex { get; set; }

        /// <summary>
        /// Enabled all children controls for absolut positioning (children have to use TileLeft, TileTop, Left or Top to position)
        /// </summary>
        public bool? IsAbsolute { get; set; }

        /// <summary>
        /// if set to try, the Control will not handle any click or touch events
        /// </summary>
        public bool? IsHitTestDisabled { get; set; }

        /// <summary>
        /// Sets the parent of the control (uses FldOrder)
        /// </summary>
        public int ParentFld { get; set; }

        /// <summary>
        /// Wraps the control in a "ctrlTileEntry” control for better Look-n-Feel in Table. This setting has not impact on Forms
        /// </summary>
        public bool? UseTE { get; set; }

        /// <summary>
        /// Prevents a control from being wrapped in a ctrlTileEntry in a Form. This Setting has no impact on Tables
        /// </summary>
        public bool? NoTE { get; set; }

        /// <summary>
        /// the Main ID of the control
        /// </summary>
        public string cdeNMID { get; set; }

        ///If Set to true, the value will always be sent from the UX to the Node - no matter if the old value was the same value
        public bool? ForceSet { get; set; }

        /// <summary>
        /// sets the width of an input field
        /// </summary>
        public int FldWidth { get; set; }

        /// <summary>
        /// Will be written in the “Value” Property if the Value property is “Null”
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Same as the Tag element in XAML used for cookies with the control
        /// </summary>
        public string Tag { get; set; }
        /// <summary>
        /// Header for a Table
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// A function if returning true will hide the control
        /// </summary>
        public string HideCondition { get; set; }

        /// <summary>
        /// a function if returning true will gray out the control
        /// </summary>
        public string GreyCondition { get; set; }

        /// <summary>
        /// This text will be shown instead of the control if the control could not be started or is still being retrieved from the mesh
        /// </summary>
        public string PlaceHolder { get; set; }

        /// <summary>
        /// Use with caution! Overwriting this can cause unexpected behavior. Only use for Special TileButton Cases: CDE_DELETE (adds a delete/Save button to a table), CDE_DETAILS (allows to switch a table to form view and back)
        /// </summary>
        public string DataItem { get; set; }

        /// <summary>
        /// If one of the NUItags is recognized, the corresponding control will get a "OnNUITag" call and can then handed the tag. It can then decide what else to do with the command
        /// </summary>
        public string NUITags { get; set; }


        /// <summary>
        /// Table Header style class of the control
        /// </summary>
        public string THClassName { get; set; }

        /// <summary>
        /// Table Cell style class of the control
        /// </summary>
        public string TCClassName { get; set; }

        /// <summary>
        /// Sets the Caption of the TileGroup. If Null or "" no header will be shown.
        /// Otherwise a ctrlSmartLabel will be used to show the title of the TG
        /// Same as the "Title" or "Label" Property
        /// </summary>
        public string Caption { get; set; }

        /// <summary>
        /// Sets the Style for Caption of the TileGroup
        /// </summary>
        public string LabelClassName { get; set; }

        /// <summary>
        /// Foreground color of the Caption Text
        /// </summary>
        public string LabelForeground { get; set; }

        /// <summary>
        /// Foreground color of the Caption Text
        /// </summary>
        public string CaptionForeground { get; set; }
        /// <summary>
        /// Sets the Font Size of a Label
        /// </summary>
        public int LabelFontSize { get; set; }
        /// <summary>
        /// can specify an ID of a HTML DIV Element where this control will be rendered in (AppendChild)
        /// </summary>
        public string RenderTarget { get; set; }

        /// <summary>
        /// Overwrite 'red' in hardcoded styles of a control
        /// </summary>
        public string Red { get; set; }

        /// <summary>
        /// Overwrite 'green' in hardcoded styles of a control
        /// </summary>
        public string Green { get; set; }

        /// <summary>
        /// sets the background color of the TileEntry
        /// </summary>
        public string MainBackground { get; set; }

        /// <summary>
        /// Allows to set the class of the TileEntry
        /// </summary>
        public string MainClassName { get; set; }

        /// <summary>
        /// Sets the class for the container of the TileEntry
        /// </summary>
        public string ContainerClassName { get; set; }
        /// <summary>
        /// Header used for the Table Column
        /// </summary>
        public string TableHeader { get; set; }

        /// <summary>
        /// Allows to add a traditional ThePropertyBag to this control.
        /// </summary>
        public ThePropertyBag MergeBag { get; set; }

        /// <summary>
        /// Adds an explainer text to wizards and other controls that support explainer
        /// </summary>
        public string Explainer { get; set; }
        /// <summary>
        /// Creator of the Property Bag
        /// </summary>
        /// <param name="c"></param>
        public static implicit operator ThePropertyBag(TheNMIBaseControl c)
        {
            var tBag = ThePropertyBag.Create(c);
            if (c.MergeBag?.Count > 0)
                tBag.MergeBag(c.MergeBag);
            return tBag;
        }

        /// <summary>
        /// General function to initialize a control
        /// </summary>
        /// <param name="pTargetControl">Parent control this control will be inserted to</param>
        /// <param name="pTRF">TFR definition for this controls</param>
        /// <param name="pPropertyBag">Propertybag of the control</param>
        /// <param name="pModelID">Model GUID of the control</param>
        /// <returns></returns>
        public virtual bool InitControl(TheNMIBaseControl pTargetControl, TheTRF pTRF, ThePropertyBag pPropertyBag, Guid? pModelID)
        {
            if (pTargetControl != null)
            {
                MyTarget = pTargetControl;
                if (MyChildren == null)
                    MyChildren = new List<TheNMIBaseControl>();
                MyTarget.MyChildren.Add(this);    //TODO: Check Memory Impact
            }
            if (pTRF != null)
            {
                MyTRF = pTRF;
                if (pTRF.FldInfo != null)
                {
                    MyFieldInfo = pTRF.FldInfo;
                    //TODO: TheNMIBaseControl.ConvertPropertiesFromBag(this.MyFieldInfo);
                }
            }
            //TODO: Put pPropertyBag in PropertyBag
            if (pModelID != null)
                MyScreenID = TheCommonUtils.CGuid(pModelID);
            return true;
        }

        /// <summary>
        /// Sets a property on the control
        /// </summary>
        /// <param name="pName">Name of the property</param>
        /// <param name="pValue">Value to be set</param>
        public virtual void SetProperty(string pName, object pValue)
        {
            if (PropertyBag == null)
                PropertyBag = new cdeConcurrentDictionary<string, object>();

            PropertyBag[pName] = pValue;
        }

        /// <summary>
        /// Adds a child to TheNMIBaseControl
        /// </summary>
        /// <param name="pChild"></param>
        public virtual void AppendChild(TheNMIBaseControl pChild)
        {
            //TODO: Not sure here!
        }

        private cdeConcurrentDictionary<string, object> PropertyBag ;
        protected Guid MyScreenID;
        protected TheTRF MyTRF ;
        protected TheFieldInfo MyFieldInfo;
        protected TheNMIBaseControl MyTarget;
        protected List<TheNMIBaseControl> MyChildren;
        protected eFieldType MyBaseType;
    }

    /// <summary>
    /// Helper Class for User Controls
    /// </summary>
    public class nmiCtrlUserControl : TheNMIBaseControl
    {
        /// <summary>
        /// Sets the ControlType of the User Control
        /// </summary>
        public new string ControlType { get; set; }
    }

    /// <summary>
    /// Propertybag for a ChartControl
    /// </summary>
    public class nmiChartControl : TheNMIBaseControl
    {
        /// <summary>
        /// Blocksize of records to be returned from a storagemirror
        /// </summary>
        public int BlockSize { get; set; }
    }

    /// <summary>
    /// This control displays the “Value” property as a vertical or horizontal bar.
    /// </summary>
    public class nmiCtrlBarChart : TheNMIBaseControl
    {
        /// <summary>
        /// sets the color of the bar
        /// </summary>
        public string Foreground { get; set; }
        /// <summary>
        /// sets the upper limit
        /// </summary>
        public int MaxValue { get; set; }
        /// <summary>
        /// sets the lower limit
        /// </summary>
        public int MinValue { get; set; }

        /// <summary>
        /// Rotates bar chart 90 degrees.
        /// </summary>
        public bool? IsVertical { get; set; }

        /// <summary>
        /// sets the background color
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// sets the foreground opacity
        /// </summary>
        public double? ForegroundOpacity { get; set; }

        /// <summary>
        /// Sets the zero mark is to the right
        /// </summary>
        public bool? IsInverted { get; set; }

        /// <summary>
        /// Sets the color of the small number label in the BarChart
        /// </summary>
        public string LabelColor { get; set; }

        /// <summary>
        /// Creates a margin around the BarChart
        /// </summary>
        public int? DrawMargin { get; set; }
    }

    /// <summary>
    /// Creates a hash-icon from the Value in the control
    /// </summary>
    public class nmiCtrlHashIcon: TheNMIBaseControl
    {
        /// <summary>
        /// Creates a margin around the HashIcon
        /// </summary>
        public int? DrawMargin { get; set; }
    }

    //public class nmiCtrlCanvasDraw : TheNMIBaseControl
    //{
    //    public bool? IsVertical { get; set; }

    //    public bool? IsInverted { get; set; }

    //    public string AddShape { get; set; }

    //    public string SetShape { get; set; }
    //}

    /// <summary>
    /// This control displays a check box with or without checkmark.
    /// </summary>
    public class nmiCtrlSingleCheck : TheNMIBaseControl
    {
        /// <summary>
        ///sets the foreground color
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// sets the background color
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// sets the title of the control
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// sets an image instead of the standard checkicon
        /// </summary>
        public string CheckImage { get; set; }

        /// <summary>
        /// if this is set, a dialog box asking the the question set in this field.
        /// The user can select yes or no - only on yes will the checkbox will be changed
        /// </summary>
        public string AreYouSure { get; set; }
    }


    /// <summary>
    /// Creates a field of check boxes representing each bit of a numeric value in binary code
    /// </summary>
    public class nmiCtrlCheckField : TheNMIBaseControl
    {
        /// <summary>
        /// A list of Images separated by ; for each checkbox of the list from right to left that will be applied to the background of the checkboxes
        /// </summary>
        public string ImageList { get; set; }

        /// <summary>
        /// Sets custom Captions for each checkbox in the Field
        /// </summary>
        public string Options { get; set; }

        /// <summary>
        /// Sets the number of bits in the checkfield
        /// </summary>
        public int Bits { get; set; }

        /// <summary>
        /// Shows the integer result of the Bitfield behind the Apply Button using this text as the label
        /// </summary>
        public string ResultLabel { get; set; }
    }


    /// <summary>
    /// Renders a snippet of HMTL5 in the boundaries of the Tile
    /// </summary>
    public class nmiCtrlFacePlate : TheNMIBaseControl
    {
        /// <summary>
        /// A url pointing a the HTML5 markup to be inserted
        /// </summary>
        public string HTMLUrl { get; set; }

        /// <summary>
        /// a string with the HTML5 to be rendered in the control
        /// </summary>
        public string HTML { get; set; }

        /// <summary>
        /// Background color of the tile
        /// </summary>
        public string Background { get; set; }
    }

    /// <summary>
    /// Creates a drawing overlay on an element
    /// </summary>
    public class nmiCtrlDrawOverlay : TheNMIBaseControl
    {
        /// <summary>
        /// Autoajusts to the element
        /// </summary>
        public bool? AutoAdjust { get; set; }
    }

    /// <summary>
    /// Allows to upload files to the Relay
    /// </summary>
    public class nmiCtrlDropUploader : TheNMIBaseControl
    {
        /// <summary>
        /// Title of the Uploaded shown in the drop area of the uploader
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Sets the maximum allowed file size to be sent to the relay
        /// </summary>
        public int MaxFileSize { get; set; }

    }

    /// <summary>
    /// Base field for all Edit Box based controls
    /// </summary>
    public class nmiCtrlSingleEnded : TheNMIBaseControl
    {
        /// <summary>
        /// Foreground color of the edit box
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// Background color of the edit box
        /// </summary>
        public string Background { get; set; }


        /// <summary>
        /// Style Class of the EditBox
        /// </summary>
        public string InnerClassName { get; set; }

        /// <summary>
        /// Sets a style on the input field not the surrounding div
        /// </summary>
        public string InnerStyle { get; set; }
        /// <summary>
        /// Formats the display of the SmartLabel used in a Table - Format has not effect in Forms
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// A RegEx expression that will be validated when the control is written
        /// </summary>
        public string Validator { get; set; }

        /// <summary>
        /// A text that is displayed as a toast when the validation failed using the Validator property
        /// </summary>
        public string ValidateErrorText { get; set; }
    }

    public class nmiCtrlPassword: nmiCtrlSingleEnded
    {
        /// <summary>
        /// Hides the Multi-Touch-Lock
        /// </summary>
        public bool? HideMTL { get; set; }

        /// <summary>
        /// If true, the password control will enforce the password rules and show a confirmation dialog. A Min TileHeight of 2 is required for the confirmation to work
        /// </summary>
        public bool? EnforceAndConfirm { get; set; }

        /// <summary>
        /// if true, the password will only be updated when a button was pushed
        /// </summary>
        public bool? RequireUpdateButton { get; set; }
    }

    public class nmiCtrlNumber : nmiCtrlSingleEnded
    {
        /// <summary>
        /// Maximum Value the number will accept
        /// </summary>
        public int MaxValue { get; set; }

        /// <summary>
        /// Minimum Value the Number will accept
        /// </summary>
        public int MinValue { get; set; }
    }

    public class nmiCtrlGauge : nmiCtrlNumber
    {
        public bool? DontAnimate { get; set; }
    }

    public class nmiCtrlHalfGauge : nmiCtrlGauge
    {
        public string SubTitle { get; set; }

        public int LowerLimit { get; set; }
        public int UpperLimit { get; set; }
    }

    /// <summary>
    /// Multi Line text field
    /// </summary>
    public class nmiCtrlTextArea : nmiCtrlSingleEnded
    {
        /// <summary>
        /// Determines how many rows a text area should have
        /// </summary>
        public int? Rows { get; set; }

    }

    /// <summary>
    /// An endless slider that can be used to increase/decrease a value
    /// </summary>
    public class nmiCtrlEndlessSlider : TheNMIBaseControl
    {

        /// <summary>
        /// Color of the slider Lines
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// Maximum Value the slider can increase to
        /// </summary>
        public int MaxValue { get; set; }

        /// <summary>
        /// Minimum Value the slider can decrease to
        /// </summary>
        public int MinValue { get; set; }

        /// <summary>
        /// Be default a horizontal slider is shown. If set to true, the slider will be shown vertically
        /// </summary>
        public bool? IsVertical { get; set; }


        /// <summary>
        /// If set to true, the slider value goes to MinValue if it was increased beyond the MaxValue and vs.
        /// </summary>
        public bool? AllowRollover { get; set; }

        /// <summary>
        /// Background color of the slider
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// how fast should values decrease/increase
        /// </summary>
        public double StepFactor { get; set; }

        /// <summary>
        /// Width of the lines
        /// </summary>
        public int LineWidth { get; set; }

        /// <summary>
        /// Space between the lines
        /// </summary>
        public int LineGap { get; set; }
    }

    /// <summary>
    /// Shows the multi-touch Login
    /// </summary>
    public class nmiCtrlMoTLock : TheNMIBaseControl
    {
        /// <summary>
        /// Sets the background color of the MutLock
        /// </summary>
        public string Background { get; set; }
    }

    /// <summary>
    /// Shows a progress bar
    /// </summary>
    public class nmiCtrlProgressBar : TheNMIBaseControl
    {
        /// <summary>
        /// Maximum value the progress bar goes to
        /// </summary>
        public int MaxValue { get; set; }

        /// <summary>
        /// Background color of the progress bar
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// color of the progress bar
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// Style Class of the progress bar
        /// </summary>
        public string BarClassName { get; set; }

    }


    /// <summary>
    /// A area a shape an be drawn into
    /// </summary>
    public class nmiCtrlShape : TheNMIBaseControl
    {
        /// <summary>
        /// Background color of the shape
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// Adds a new shape to the area
        /// </summary>
        public string AddShape { get; set; }

        /// <summary>
        /// Sets a shape in the area after clearing the area
        /// </summary>
        public string SetShape { get; set; }

        /// <summary>
        /// if set to true, the area will fire "OnPointerMove" events
        /// </summary>
        public bool? EnableMouseMove { get; set; }

        /// <summary>
        /// If set to true, the area will send pointer events via "UPDATE_POINTER" to the service Relay owning the control
        /// </summary>
        public bool? SendPointer { get; set; }
    }

    public class nmiCtrlDateTime : TheNMIBaseControl
    {
        /// <summary>
        /// if set to true, only the date can be picked - otherwise date and time can be picked
        /// </summary>
        public bool? DateOnly { get; set; }

        /// <summary>
        /// If true, the picker will show a calendar instead of a scroll selector
        /// </summary>
        public bool? UseCalendar { get; set; }

        /// <summary>
        /// If true, all times will be shown in 24h mode instead of AM/PM
        /// </summary>
        public bool? Use24h { get; set; }

        /// <summary>
        /// If true in conjunction with TimeSpan will include days
        /// </summary>
        public bool? IncludeDays { get; set; }
    }
    public class nmiCtrlCertPicker : TheNMIBaseControl
    {
    }

    public class nmiCtrlPropertyPicker: TheNMIBaseControl
    {
        /// <summary>
        /// If true, the property picker allows to select multiple properties and returns a string with the properties separated by the "Separator"
        /// </summary>
        public bool? AllowMultiSelect { get; set; }

        /// <summary>
        /// Changes the default separator ; to anything else
        /// </summary>
        public string Separator { get; set; }

        /// <summary>
        /// Sets the field number that contains the ThingID. The Thing must be selected before the property picker can be used
        /// </summary>
        public int ThingFld { get; set; }

        /// <summary>
        /// Instead of using a single input text, this property picker will use a multi-line Text Field
        /// </summary>
        public int? MultiLines { get; set; }

        /// <summary>
        /// Includes cde System Properties in return
        /// </summary>
        /// <value><c>null</c> if [system properties] contains no value, <c>true</c> if [system properties]; otherwise, <c>false</c>.</value>
        public bool? SystemProperties { get; set; }
    }

    public class nmiCtrlDeviceTypePicker : TheNMIBaseControl
    {
        /// <summary>
        /// Includes Remote Things
        /// </summary>
        public bool? IncludeRemotes { get; set; }

        /// <summary>
        /// Allows to specify a filter for the ThingPicker PropertyName=Value. The value will be interpreted as "startswith"
        /// </summary>
        public string Filter { get; set; }
    }

    public class nmiCtrlThingPicker: TheNMIBaseControl
    {
        /// <summary>
        /// If this is set to another property of the Thing, the ThingPicker will show available properties and allows to pick thing + property in one swoop
        /// </summary>
        public string PropertyTarget { get; set; }

         /// <summary>
         /// If set, only things of this engine will be displayed
         /// </summary>
        public string EngineFilter { get; set; }

        /// <summary>
        /// Includes Remote Things
        /// </summary>
        public bool? IncludeRemotes { get; set; }

        /// <summary>
        /// Includes IBaseEngines
        /// </summary>
        public bool? IncludeEngines { get; set; }

        /// <summary>
        /// Allows to specify a filter for the ThingPicker PropertyName=Value. The value will be interpreted as "startswith"
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// If false the picker will return the content of this property instead of cdeMID
        /// </summary>
        /// <value>The name of the value as.</value>
        public string ValueProperty { get; set; }
    }

    /// <summary>
    /// A Dropdown multiple choice object to select one of many options
    /// </summary>
    public class nmiCtrlComboBox : TheNMIBaseControl
    {
        /// <summary>
        /// Options of the Combobox separated by ;
        /// if value and option need to be different use "Option":"Value"
        /// if grouping is desired separate the grooups with ";:;"
        /// For the ThingPicker use "LOOKUP:THINGPICKER" RETIRED: Please use nmiCtrlThingPicker
        /// for the PropertyPicker use "LOOKUP:PROPERTYPICKER:ThingGUID" RETIRED: please use nmiCtrlPropertyPicker
        /// </summary>
        public string Options { get; set; }

        /// <summary>
        /// If a group uses an integer this lookup can be used to find a matching test for the group #
        /// </summary>
        public string GroupLookup { get; set; }

        /// <summary>
        /// Allows to set a simple filter on the result of the lookup. syntax: FldName=FldValue
        /// </summary>
        public string Filter { get; set; }
        /// <summary>
        /// ComboBox theme
        /// </summary>
        public string Theme { get; set; }

        /// <summary>
        /// Backgroundcolor of the surrounding Tile
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// Text color of the ComboBox
        /// </summary>
        public string Foreground { get; set; }
        /// <summary>
        /// Style Class of the Edit box belonging to the ComboBox
        /// </summary>
        public string InnerClassName { get; set; }

        /// <summary>
        /// If true, the value can only be set ONCE. (i.e. DeviceType)
        /// </summary>
        public bool? WriteOnce { get; set; }

        /// <summary>
        /// Display this text instead of the real value in the field
        /// </summary>
        public string DisplayField { get; set; }

        /// <summary>
        /// Changes the default separator ; to anything else
        /// </summary>
        public string Separator { get; set; }
    }

    public class nmiCtrlComboLookup : TheNMIBaseControl
    {
        /// <summary>
        /// if set, the storage service might be in another plugin
        /// </summary>
        public string ModelID { get; set; }

        /// <summary>
        /// Select the GUID of the StorageTable you want to load
        /// </summary>
        public string StorageTarget { get; set; }

        /// <summary>
        /// Select the PropertyName/FieldName that contains the Text to select the value
        /// </summary>
        public string NameFld { get; set; }

        /// <summary>
        /// Select the PropertyName/FieldName that contains the Value
        /// </summary>
        public string ValueFld { get; set; }

        /// <summary>
        /// Define a Group for the Select. If not set, the Selector will not have a group
        /// </summary>
        public string GroupFld { get; set; }

        /// <summary>
        /// if true, the lookup reloads the content from the StorageMirror when loaded
        /// </summary>
        public bool? RefreshOnLoad { get; set; }


        /// <summary>
        /// if true an empty "CDE_NOP" entry will be added to select a blank entry
        /// </summary>
        public bool? AddEmptyEntry { get; set; }
    }

    /// <summary>
    /// Control Type: Container
    /// Main Button Control of the C-DEngine - it can be used as a container for other controls by setting the ParentFld to the FldOrder of the TileButton
    /// Its recommended to set the TileWidth and TileHeight of the Button to set the size.
    /// If TW or TH the hight or width is meassured by the controls inside.
    /// </summary>
    public class nmiCtrlTileButton : TheNMIBaseControl
    {
        /// <summary>
        /// If set to true, this button is the main refresher of the form
        /// </summary>
        public bool? IsRefresh { get; set; }
        /// <summary>
        /// If set the Button works in a scrollable TileGroup
        /// </summary>
        public bool? IgnoreHitTarget { get; set; }
        /// <summary>
        /// Sets the background of the button
        /// </summary>
        public string Background { get; set; }


        /// <summary>
        /// Sets the foreground color of the button title
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// displays an image as Thumbnail on the button
        /// </summary>
        public string Thumbnail { get; set; }

        /// <summary>
        /// Allows formatting of the title similar to C# string.format()
        /// </summary>
        public string Format { get; set; }
        /// <summary>
        /// Allows to just tap the button
        /// </summary>
        public bool? EnableTap { get; set; }

        /// <summary>
        /// Title of the button
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Function of code executed OnClick
        /// </summary>
        public string OnClick { get; set; }

        /// <summary>
        /// Function of code executed OnTileDown
        /// </summary>
        public string OnTileDown { get; set; }

        /// <summary>
        /// A Url pointing a the HTML5 markup to be inserted
        /// </summary>
        public string HTMLUrl { get; set; }

        /// <summary>
        /// a string with the HTML5 to be rendered in the control
        /// </summary>
        public string HTML { get; set; }

        /// <summary>
        /// Tab Order in a screen
        /// </summary>
        public int? TabIndex { get; set; }

        /// <summary>
        /// if this is set, a dialog box asking the the question set in this field.
        /// The user can select yes or no - only on yes will the OnClick be executed
        /// </summary>
        public string AreYouSure { get; set; }


        /// <summary>
        /// defines a group of collapsible groups to be toggeled by a button. Syntax: "GroupName:ID"
        /// If a user clickes a button with a set GroupID, all Collabpsible groups in the GroupName will be closed the one with the ID will be opened
        /// </summary>
        public string GroupID { get; set; }

        /// <summary>
        /// Allows to define a style classname for a hover action on a button. If not set, the default style "cdeButtonHover" will be used.
        /// </summary>
        public string HoverClassName { get; set; }

        /// <summary>
        /// If true, this button will submit a form/wizard that has "IsPostingOnSubmit" enabled
        /// </summary>
        public bool? IsSubmit { get; set; }
    }

    public class nmiDashboard : TheNMIBaseControl
    {
        /// <summary>
        /// Sets the background of the button
        /// </summary>
        public string Background { get; set; }


        /// <summary>
        /// Sets the foreground color of the button title
        /// </summary>
        public string Foreground { get; set; }
        /// <summary>
        /// Allows to set a format for the caption of the Button in the main Dashboard
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Allows to set a format for the caption of the Screen
        /// </summary>
        public string LabelFormat { get; set; }

        /// <summary>
        /// Category of the DashBoard
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Forces the Dashboard to be loaded on initial MetaLoad
        /// </summary>
        public bool? ForceLoad { get; set; }

        /// <summary>
        /// Hides the pins on a Dashboard
        /// </summary>
        public bool? HidePins { get; set; }

        /// <summary>
        /// Sets a custom name for the new Sidebar
        /// </summary>
        public string SideBarTitle { get; set; }

        /// <summary>
        /// Sets a valid Font-Awesome icon for the sidebar
        /// </summary>
        public string SideBarIconFA { get; set; }

        /// <summary>
        /// Hides the Show All button in the dashboard
        /// </summary>
        public bool? HideShowAll { get; set; }

        /// <summary>
        /// Sets a dynamic Faceplate for the plugin
        /// </summary>
        public string HTMLUrl { get; set; }

        /// <summary>
        /// Sets a static Faceplate for the plugin
        /// </summary>
        public string StaticHTMLUrl { get; set; }

        /// <summary>
        /// adds a Thumbnail to the Plugin Tile
        /// </summary>
        public string Thumbnail { get; set; }
    }
    public class nmiScreen : TheNMIBaseControl
    {
        /// <summary>
        /// Hides the pins on a Screen
        /// </summary>
        public bool? HidePins { get; set; }
    }

    /// <summary>
    /// Propertybag Creator for Dashboard Tiles
    /// </summary>
    public class nmiDashboardTile : TheNMIBaseControl
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Sets a subtitle to a dashicon </summary>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public string SubTitle { get; set; }
        /// <summary>
        /// displays an image as Thumbnail on the button
        /// </summary>
        /// <value>The tile thumbnail.</value>
        public string TileThumbnail { get; set; }
        /// <summary>
        /// displays an image as Thumbnail on the button
        /// </summary>
        public string Thumbnail { get; set; }
        /// <summary>
        /// displays an image as Thumbnail on the button
        /// </summary>
        [Obsolete("Please use Thumbnail instead - this option will be removed")]
        public string ThumbNail { get; set; }

        /// <summary>
        /// Describes the screen for the ScreenPicker
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// Allows to set a format for the caption of the Button in the main Dashboard
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Allows to set a format for the caption of the Screen
        /// </summary>
        public string LabelFormat { get; set; }

        /// <summary>
        /// Allows to set an HTML5 FacePlate to the tileButton
        /// </summary>
        public string HTMLUrl { get; set; }

        /// <summary>
        /// Allows to set inline HTML5 markup
        /// </summary>
        public string HTML { get; set; }

        /// <summary>
        /// Allows to overwrite the category classname (default "cdeTiles") with any other class
        /// </summary>
        public string CategoryClassName { get; set; }

        /// <summary>
        /// Sets the classname for the category of a DashPanelItem
        /// </summary>
        public string CategoryLabelClassName { get; set; }
        /// <summary>
        /// Allows to define a style classname for a hover action on a button. If not set, the default style "cdeButtonHover" will be used.
        /// </summary>
        public string HoverClassName { get; set; }

        /// <summary>
        /// if set to True, the screen behind the Tile has to be reloaded if this tile was reloaded (Reload Screen Behind)
        /// </summary>
        public bool RSB { get; set; }

        /// <summary>
        /// sets the color of the background on the Dashboard Tile Button
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// sets the color of the text on the Dashboard Tile Button
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// Sets the category of the DashboardIcon
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Hides the pins on a Form
        /// </summary>
        public bool? HidePins { get; set; }

        /// <summary>
        /// Hides only the pinning Pins
        /// </summary>
        public bool? HidePinPins { get; set; }
        /// <summary>
        /// Forces the NMI to load the screen behind the DashboardTile to be loaded immediately
        /// </summary>
        public bool? ForceLoad { get; set; }

        /// <summary>
        /// Does not show the Screen in the Side Bar
        /// </summary>
        public bool? HideFromSideBar { get; set; }

        /// <summary>
        /// The referenced screen is a popup
        /// </summary>
        public bool? IsPopup { get; set; }
    }


    /// <summary>
    /// Control Type: Container
    /// allows to group controls together in a Group of elements by setting the ParentFld to the FldOrder of the TileGroup
    /// Its recommended to set the TileWidth and TileHeight of the Button to set the size.
    /// If TW or TH the height or width is measured by the controls inside
    /// </summary>
    public class nmiCtrlTileGroup : TheNMIBaseControl
    {
        /// <summary>
        /// OnClick event on the control
        /// </summary>
        public string OnClick { get; set; }
        /// <summary>
        /// Background of the TileGroup
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// if true, the tile group is just a div
        /// </summary>
        public bool? IsDivOnly { get; set; }
        /// <summary>
        /// If true, the TileGroup can be scrolled horizontally
        /// </summary>
        public bool? IsHScrollable { get; set; }

        /// <summary>
        /// if true, the TileGroup can be scrolled vertically
        /// </summary>
        public bool? IsVScrollable { get; set; }

        /// <summary>
        /// Can be "hidden" or "visible". Default is "hidden"
        /// </summary>
        public string Overflow { get; set; }

        /// <summary>
        /// Formats the Caption of the Group similar to string.format() in C#
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Allows to set a format for the caption of the Screen
        /// </summary>
        public string LabelFormat { get; set; }

        /// <summary>
        /// Color of the Group Background (without Title)
        /// </summary>
        public string GroupBackground { get; set; }
    }

    /// <summary>
    /// Wraps all child controls in group that can be collapsed
    /// </summary>
    public class nmiCtrlCollapsibleGroup : nmiCtrlTileGroup
    {
        /// <summary>
        /// Background color of the Caption of the group
        /// </summary>
        public string CaptionBackground { get; set; }

        /// <summary>
        /// Sets the color of the TileGroup Foreground
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// if set to true, the group will be collapsed OnLoad
        /// </summary>
        public bool? DoClose { get; set; }

        /// <summary>
        /// Hides the Pins of the Collapsible Group
        /// </summary>
        public bool? HidePins { get; set; }

        /// <summary>
        /// If set to true, the header of the Collapsible Group will be 1/2 TileHeight
        /// default class is: cdeTileGroupHeaderSmall or cdeTileGroupHeader
        /// </summary>
        public bool? IsSmall { get; set; }


        /// <summary>
        /// if set, the Collapsible group will add arrows to allow expanding the group to the left and right. No smaller than 6 Tiles and no wider than MaxTileWidth
        /// </summary>
        public bool? AllowHorizontalExpand { get; set; }

        /// <summary>
        /// Creates a quarter-tilesize margin around the collapsible groups
        /// </summary>
        public bool? UseMargin { get; set; }
    }


    /// <summary>
    /// The base wrapper control for Forms and Tables in the NMI
    /// </summary>
    public class nmiCtrlTileEntry : TheNMIBaseControl
    {
    }


    /// <summary>
    /// Shows the default About Button
    /// </summary>
    public class nmiCtrlAboutButton : TheNMIBaseControl
    {
        /// <summary>
        /// Sets the foreground color on all text elements
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// sets the background color of the about box
        /// if set to black, the foreground will be automatically set to white
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// Title of the About Box
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Subtitle
        /// </summary>
        public string SubTitle { get; set; }
        /// <summary>
        /// Describes the service
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Copyrights
        /// </summary>
        public string Copyright { get; set; }

        /// <summary>
        /// Version info
        /// </summary>
        public string Version { get; set; }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Node of the plugin </summary>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public string NodeText { get; set; }
        /// <summary>
        /// Author
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Icon to be used in the about box
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Icon Text
        /// </summary>
        public string IconText { get; set; }

        /// <summary>
        /// Additional Text to be displayed
        /// </summary>
        public string AdText { get; set; }

        /// <summary>
        /// Additional status text
        /// </summary>
        public string StatusText { get; set; }

        /// <summary>
        /// Status message (should be MyBaseThing.LastMessage)
        /// </summary>
        public string LastMessage { get; set; }

        /// <summary>
        /// Control type of the about box
        /// </summary>
        public override string ControlType { get { return "cdeNMI.ctrlAboutButton"; } }
    }

    /// <summary>
    /// Displays a YesNo Combobox
    /// </summary>
    public class nmiCtrlYesNo : nmiCtrlComboBox
    {
        /// <summary>
        /// If set to true, a "N/A" option is added
        /// </summary>
        public bool? InlcudeNA { get; set; }
    }

    /// <summary>
    /// Shows an area that can be multitouched and inked on
    /// </summary>
    public class nmiCtrlTouchDraw : TheNMIBaseControl
    {
        /// <summary>
        /// if set to true, the control will send NEWDRAWOBJECT to all nodes
        /// </summary>
        public bool? IsSynced { get; set; }
        /// <summary>
        /// Shows the save button
        /// </summary>
        public bool? ShowSave { get; set; }

        /// <summary>
        /// shows the play button
        /// </summary>
        public bool? ShowPlay { get; set; }

        /// <summary>
        /// Shows the color button
        /// </summary>
        public bool? ShowColors { get; set; }

        /// <summary>
        /// sets the background color of the about box
        /// if set to black, the foreground will be automatically set to white
        /// </summary>
        public string Background { get; set; }

    }

    /// <summary>
    /// A control wrapper playing Videos
    /// </summary>
    public class nmiCtrlVideoViewer : TheNMIBaseControl
    {
        /// <summary>
        /// Sets the background color of the video
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// If set to true, a local web-camera can be displayed in the Video Viewer (only very few viewers support this)
        /// </summary>
        public bool? ShowCam { get; set; }

        /// <summary>
        /// A url pointing at the source of the Video
        /// If set to "LIVE" the web-camera live stream is used
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Shows or hides the video controls
        /// </summary>
        public bool? ShowControls { get; set; }
    }

    /// <summary>
    /// Show an Image
    /// </summary>
    public class nmiCtrlPicture : TheNMIBaseControl
    {
        /// <summary>
        /// OnClick event on the control
        /// </summary>
        public string OnClick { get; set; }
        /// <summary>
        /// Sets the background of the tile
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// Enables four zoom stages of the image. 4th is the biggest
        /// </summary>
        public bool? EnableZoom { get; set; }

        /// <summary>
        /// Source of the Image. For dynamic images use Value
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// If the image is a filmstrip, writing true in this property will start/autostart the strip
        /// </summary>
        public bool? StartSequence { get; set; }

        /// <summary>
        /// Plays the filmstrip in a loop
        /// </summary>
        public bool? DoLoop { get; set; }

        /// <summary>
        /// Sets the last sequence number for the film strip. Images must be named "NAMExxxxx".png".
        /// Name is set by "Value" or "Source" and the xxxxx will be replaced by a number fom 0 to LastSeqNo
        /// </summary>
        public int LastSeqNo { get; set; }
        /// <summary>
        /// Set the initial Zoomlevel (0-3)
        /// </summary>
        public int ZoomLevel { get; set; }

        /// <summary>
        /// Set the Width of the image in pixels for zoom level 0
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Sets the height of the image in pixels for zoom level 0
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Set the width of image for zoom level 3 (naturalwidth)
        /// </summary>
        public int FullWidth { get; set; }

        /// <summary>
        /// Set the height of image for zoom level 3 (naturalheight)
        /// </summary>
        public int FullHeight { get; set; }

        /// <summary>
        /// Sets the "alt" tag of the image
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Sets the opacity of the image
        /// </summary>
        public double? ImageOpacity { get; set; }

        /// <summary>
        /// Adjusts the size of the image to the given TW/TH container
        /// </summary>
        public bool? AutoAdjust { get; set; }

        /// <summary>
        /// Any string send to "Value" of a picture control is interpreted as "blob" and not as a URL. If false, the value is checked if its larger then 512 characters and than assumed to be a blob
        /// </summary>
        public bool? IsBlob { get; set; }

        /// <summary>
        /// Allows to overwrite the default image format "jpeg" for blobs
        /// </summary>
        public string ImgFormat { get; set; }
    }


    /// <summary>
    /// Table Control
    /// </summary>
    public class nmiCtrlTableView : TheNMIBaseControl
    {
        /// <summary>
        /// if true, the tile group is just a div
        /// </summary>
        public bool? IsDivOnly { get; set; }
        /// <summary>
        /// Style Class for the THEAD
        /// </summary>
        public string HeaderClassName { get; set; }

        /// <summary>
        /// Style class for the TABLE (Default is "CMyTable")
        /// </summary>
        public string TableClassName { get; set; }

        /// <summary>
        /// Style Class for the Table Name in the Header
        /// </summary>
        public string TNClassName { get; set; }

        /// <summary>
        /// Style Class for the Add Button in the Header
        /// </summary>
        public string AddButtonClassName { get; set; }

        /// <summary>
        /// if true the table accepts files to be uploaded
        /// </summary>
        public bool? IsDropTarget { get; set; }

        /// <summary>
        /// if true the uploaded files will be pushed with PublishCentral.
        /// </summary>
        public bool? AllowGlobalPush { get; set; }

        /// <summary>
        /// Sets the maximum allowed file size to be sent to the relay
        /// </summary>
        public int? MaxFileSize { get; set; }

        /// <summary>
        /// Shows a Filter Field in the header to filter the table by
        /// </summary>
        public bool? ShowFilterField { get; set; }

        /// <summary>
        /// Points at the Form used as the Template to edit rows in this table
        /// </summary>
        public string TemplateID { get; set; }
    }

    /// <summary>
    /// Creates a smartlabel/Textoutput field that is a customizable output field
    /// </summary>
    public class nmiCtrlSmartLabel : TheNMIBaseControl
    {
        /// <summary>
        /// Sets the background of the label
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// Sets the foreground color of the label
        /// </summary>
        public string Foreground { get; set; }

        /// <summary>
        /// Allows to format the text of the label similar to C# string.format()
        /// </summary>
        public string Format { get; set; }


        /// <summary>
        /// Sets the base Element used for the label (HTML5 only)
        /// </summary>
        public string Element { get; set; }

        /// <summary>
        /// Even if the smart label can be converted to a control. if this is set to true, the label is read only
        /// </summary>
        public bool? IsReadOnly { get; set; }

        /// <summary>
        /// Can be used to set a static text of the smartLabel. It will be overwritten if the Value Property is set
        /// </summary>
        public string Text { get; set; }

        public string ValueTitle { get; set; }
        public int ValueTitleSize { get; set; }
        public string ValueTitleColor { get; set; }
    }

    /// <summary>
    /// Connectivity Block Properties
    /// </summary>
    public class nmiConnectivityBlock : TheNMIBaseControl
    {
        /// <summary>
        /// Property Name for "connected"
        /// </summary>
        public string ConnectedPropertyName { get; set; }
        /// <summary>
        /// Property name for "AutoConnect"
        /// </summary>
        public string AutoConnectPropertyName { get; set; }
    }

    /// <summary>
    /// Start Block properties
    /// </summary>
    public class nmiStartingBlock : TheNMIBaseControl
    {
        /// <summary>
        /// Property Name for started
        /// </summary>
        public string StartedPropertyName { get; set; }
        /// <summary>
        /// Propoertyname for AutoStart
        /// </summary>
        public string AutoStartPropertyName { get; set; }
    }

    /// <summary>
    /// Properties for Dynamic
    /// </summary>
    public class nmiDynamicProperty : TheNMIBaseControl
    {
        /// <summary>
        /// Allows to predefine certain dynamic properties that can be added
        /// </summary>
        public string Options { get; set; }

        /// <summary>
        /// Defines options that should show as password/secure entry
        /// </summary>
        public string SecureOptions { get; set; }

        /// <summary>
        /// Title for the new property/setting (default: "Property")
        /// </summary>
        public string ToAddName { get; set; }
    }

    /// <summary>
    /// Properties for Standard Forms
    /// </summary>
    public class nmiStandardForm : TheNMIBaseControl
    {
        /// <summary>
        /// Name of a property that updates the icon
        /// </summary>
        public string IconUpdateName { get; set; }
        /// <summary>
        /// Category the form should be listed under
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// If true, the form will use margins between the fields
        /// </summary>
        public bool? UseMargin { get; set; }
    }
}