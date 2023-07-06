using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Interface of OPC UA Connector Methods a Twin might call on an OPC UA Client Connector
    /// </summary>
    public interface ICDEUAConnector
    {
        bool UAWriteSync(List<cdeP> pProps);
        bool UAReadSync(List<cdeP> pProps);
        bool UAExecuteCommand(List<cdeP> pCommandProperties);
    }

    /// <summary>
    /// Use this Base Variable Type for any Thing property that requires sub-properties for example in case of complex types in placeholders
    /// </summary>
    public class UABaseVariableType : TheDataBase 
    {
        protected ICDEProperty MyRootProperty;
        protected ICDEProperty MyRoot;
        protected string MyRootPropertyName;

        /// <summary>
        /// Constructor of the UABaseVariableType
        /// </summary>
        /// <param name="pRootProperty">The root property this class will be attached to</param>
        /// <param name="pCDEPName">The CDE Property Name (cdeP) of this class</param>
        public UABaseVariableType(ICDEProperty pRootProperty, string pCDEPName) 
        {
            MyRoot = pRootProperty;
            MyRootPropertyName = pCDEPName;
            MyRootProperty=MyRoot.GetProperty(pCDEPName, true);
        }
        /// <summary>
        /// The "real" value of the class. It will be shown in the root of the OPC Tree
        /// </summary>
        [IgnoreDataMember]
        public object Value { 
            get { return MyRoot?.GetProperty(MyRootPropertyName, true); }
            set { MyRoot?.SetProperty(MyRootPropertyName, value); }
        }

        /// <summary>
        /// Allows to Get a property on the RootProperty of this class
        /// </summary>
        /// <param name="pName">if null uses the member that calls this function</param>
        /// <returns></returns>
        public object MemberGetProperty([CallerMemberName] string pName = null)
        {
            return MyRootProperty?.GetProperty(pName,true);
        }

        /// <summary>
        /// Allows to Set a property on the RootProperty of this class
        /// </summary>
        /// <param name="pValue">Value to set</param>
        /// <param name="pName">if null uses the member that calls this function</param>
        /// <returns></returns>
        public object MemberSetProperty(object pValue, [CallerMemberName] string pName = null)
        {
            return MyRootProperty?.SetProperty(pName, pValue);
        }

        /// <summary>
        /// Allows to render a NMI form for this Class
        /// </summary>
        /// <param name="pBaseThing">Owner Thing</param>
        /// <param name="pTargetForm">Target Form</param>
        /// <param name="StartFld">Start Field Number</param>
        /// <returns></returns>
        public virtual Dictionary<string, TheFileInfo> RenderNMI(TheThing pBaseThing, TheFormInfo pTargetForm, int StartFld)
        {
            return new Dictionary<string, TheFileInfo>();
        }
    }

    /// <summary>
    /// Thing Base for an OPC UA Digital Twin
    /// </summary>
    public class TheUATwinBase : TheThingBase
    {
        public const string OPC_Method = "OPC-METHOD";
        public const string OPC_Read_Now = "OPC-READ-NOW";
        public const string OPC_Get_TimeSeries = "OPC-GET-TIMESERIES";

        ICDEUAConnector _MyBaseUAConnector;
        public ICDEUAConnector MyBaseUAConnector
        {
            get { return _MyBaseUAConnector; }
            set
            {
                _MyBaseUAConnector = value;
                HasOPCConnection = true;
            }
        }
        public TheUATwinBase(TheThing pThing, string pID)
        {
            MyBaseThing = pThing ?? new TheThing();
            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = pID;
            MyBaseThing.SetIThingObject(this);
        }
        public bool HasOPCConnection
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
    }


    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public class OPCUATypeAttribute : Attribute
    {
        public string UATypeNodeId;
        public string UANodeIdentifier;
        public string UANamespace;
        public bool AllowWrites;

        public OPCUATypeAttribute(string pType, string pNamespace)
        {
            UANodeIdentifier = pType;
            UANamespace = pNamespace;
        }
        public OPCUATypeAttribute() { }
        public OPCUATypeAttribute(string pUATypeNodeId)
        {
            UATypeNodeId = pUATypeNodeId;
        }

        public static bool ApplyUAAttributes(Type pThingType, TheThing pThing)
        {
            if (pThing == null || pThingType == null)
                return false;
            bool bResult = false;
            try
            {
                var info = (OPCUATypeAttribute)pThingType.GetCustomAttributes(typeof(OPCUATypeAttribute), true)?.FirstOrDefault();
                if (info != null)
                {
                    if (!string.IsNullOrEmpty(info?.UATypeNodeId))
                        pThing.SetProperty(nameof(UATypeNodeId), info?.UATypeNodeId);
                    if (!string.IsNullOrEmpty(info?.UANodeIdentifier))
                        pThing.SetProperty(nameof(UANodeIdentifier), info?.UANodeIdentifier);
                    if (!string.IsNullOrEmpty(info?.UANamespace))
                        pThing.SetProperty(nameof(UANamespace), info?.UANamespace);
                    pThing.SetProperty(nameof(AllowWrites), info.AllowWrites);

                    OPCUAPropertyAttribute[] psets = (OPCUAPropertyAttribute[])pThingType.GetCustomAttributes(typeof(OPCUAPropertyAttribute), true);
                    if (psets?.Any() == true)
                    {
                        foreach (var pset in psets)
                        {
                            if (!string.IsNullOrEmpty(pset?.cdePName))
                            {
                                SetUAProperty(pset, pThing.GetProperty(pset.cdePName, pset.UAMandatory));
                            }
                        }
                    }
                    OPCUAVariableAttribute[] psetsV = (OPCUAVariableAttribute[])pThingType.GetCustomAttributes(typeof(OPCUAVariableAttribute), true);
                    if (psetsV?.Any() == true)
                    {
                        foreach (var pset in psetsV)
                        {
                            if (!string.IsNullOrEmpty(pset?.cdePName))
                            {
                                SetUAVariable(pset, pThing.GetProperty(pset.cdePName, pset.UAMandatory));
                            }
                        }
                    }
                    OPCUAHAVariableAttribute[] psetsHA = (OPCUAHAVariableAttribute[])pThingType.GetCustomAttributes(typeof(OPCUAHAVariableAttribute), true);
                    if (psetsHA?.Any() == true)
                    {
                        foreach (var pset in psetsHA)
                        {
                            if (!string.IsNullOrEmpty(pset?.cdePName))
                            {
                                SetUAHAVariable(pset, pThing.GetProperty(pset.cdePName, pset.UAMandatory));
                            }
                        }
                    }

                    var thingTypeProps = pThingType.GetProperties();
                    foreach (var prop in thingTypeProps)
                    {
                        ApplyUAPropertyAttributes(pThing, prop);
                    }

                    bResult = true;
                }
            }
            catch
            {
                //ignored
            }

            return bResult;
        }

        internal static void ApplyUAPropertyAttributes(TheThing pThing, cdeP pProperty)
        {
            if (string.IsNullOrEmpty(pProperty?.Name)) return;
            var thingTypeProps = pThing?.GetObject()?.GetType()?.GetProperty(pProperty.Name);
            if (thingTypeProps != null)
                ApplyUAPropertyAttributes(pThing, thingTypeProps);
        }

        private static void ApplyUAPropertyAttributes(TheThing pThing, PropertyInfo prop)
        {
            if (pThing == null)
                return;
            try
            {
                OPCUAPropertyAttribute uaAttribute = prop.GetCustomAttributes(typeof(OPCUAPropertyAttribute), true).FirstOrDefault() as OPCUAPropertyAttribute;
                if (uaAttribute != null)
                {
                    SetUAProperty(uaAttribute, pThing.GetProperty(prop.Name, uaAttribute.UAMandatory));
                }
                OPCUAVariableAttribute uaVariAttribute = prop.GetCustomAttributes(typeof(OPCUAVariableAttribute), true).FirstOrDefault() as OPCUAVariableAttribute;
                if (uaVariAttribute != null)
                {
                    SetUAVariable(uaVariAttribute, pThing.GetProperty(prop.Name, uaVariAttribute.UAMandatory));
                }
                OPCUAHAVariableAttribute uaHAVariAttribute = prop.GetCustomAttributes(typeof(OPCUAHAVariableAttribute), true).FirstOrDefault() as OPCUAHAVariableAttribute;
                if (uaHAVariAttribute != null)
                {
                    SetUAHAVariable(uaHAVariAttribute, pThing.GetProperty(prop.Name, uaHAVariAttribute.UAMandatory));
                }
            }
            catch
            {
                TheBaseAssets.MySYSLOG.WriteToLog(7692, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("ThingService", $"Failed to Apply UA Attribute to Property: {prop?.Name}", eMsgLevel.l1_Error));
            }
        }

        private static void SetUAHAVariable(OPCUAHAVariableAttribute uaHAVariAttribute, cdeP tProp)
        {
            if (tProp != null && uaHAVariAttribute != null)
            {
                tProp.cdeM = OPCUAHAVariableAttribute.Meta;
                tProp.SetProperty(nameof(OPCUAHAVariableAttribute.Historizing), uaHAVariAttribute.Historizing);
                if (uaHAVariAttribute.MinimumSamplingInterval != 0)
                    tProp?.SetProperty(nameof(OPCUAHAVariableAttribute.MinimumSamplingInterval), uaHAVariAttribute.MinimumSamplingInterval);
                if (uaHAVariAttribute.MaximumSamplingInterval != 0)
                    tProp?.SetProperty(nameof(OPCUAHAVariableAttribute.MaximumSamplingInterval), uaHAVariAttribute.MaximumSamplingInterval);
                if (uaHAVariAttribute.ArchiveStart != DateTimeOffset.MinValue)
                    tProp?.SetProperty(nameof(OPCUAHAVariableAttribute.ArchiveStart), uaHAVariAttribute.ArchiveStart);
            }
        }

        private static void SetUAVariable(OPCUAVariableAttribute uaVariAttribute, cdeP tProp)
        {
            if (tProp != null && uaVariAttribute != null)
            {
                tProp.cdeM = OPCUAVariableAttribute.Meta;
                tProp.SetHighSpeed(true);
                tProp.SetProperty(nameof(OPCUAVariableAttribute.IsVariable), uaVariAttribute.IsVariable);
            }
        }

        private static void SetUAProperty(OPCUAPropertyAttribute uaAttribute, cdeP tProp)
        {
            if (tProp != null && uaAttribute != null)
            {
                tProp.cdeM = OPCUAPropertyAttribute.Meta;
                if (!string.IsNullOrEmpty(uaAttribute?.UABrowseName))
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UABrowseName), uaAttribute.UABrowseName);
                if (!string.IsNullOrEmpty(uaAttribute?.UASourceType))
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UASourceType), uaAttribute.UASourceType);
                if (!string.IsNullOrEmpty(uaAttribute?.UAUnits))
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UAUnits), uaAttribute.UAUnits);
                if (!string.IsNullOrEmpty(uaAttribute?.UADescription))
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UADescription), uaAttribute.UADescription);
                if (!string.IsNullOrEmpty(uaAttribute?.UATypeNodeId))
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UATypeNodeId), uaAttribute.UATypeNodeId);
                if (!string.IsNullOrEmpty(uaAttribute?.UADisplayName))
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UADisplayName), uaAttribute.UADisplayName);
                if (uaAttribute.UARangeMin != 0)
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UARangeMin), uaAttribute.UARangeMin);
                if (uaAttribute.UARangeMax != 0)
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UARangeMax), uaAttribute.UARangeMax);
                if (uaAttribute.UAWriteMask != 0)
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UAWriteMask), uaAttribute.UAWriteMask);
                if (uaAttribute.UAUserWriteMask != 0)
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UAUserWriteMask), uaAttribute.UAUserWriteMask);
                if (uaAttribute.AllowWrites)
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.AllowWrites), uaAttribute.AllowWrites);
                if (uaAttribute.HideFromAnonymous)
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.HideFromAnonymous), uaAttribute.HideFromAnonymous);
                if (uaAttribute.UAMandatory)
                    tProp?.SetProperty(nameof(OPCUAPropertyAttribute.UAMandatory), uaAttribute.UAMandatory);
            }
        }
    }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
    public class OPCUABaseAttribute : Attribute
    {
        public const string Meta = "UATag";
        public string UABrowseName;
        public string cdePName;

        public string UADescription;
        public string UADisplayName;
        public string UATypeNodeId;
        public bool HideFromAnonymous;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class OPCUAPropertyAttribute : OPCUABaseAttribute
    {
        public new const string Meta = "UAProperty";

        // Possible future enhancements. All these should be defined in the UA NodeSet referenced in the UATypeNodeId of TheThing
        public string UASourceType;
        public string UAUnits;
        public double UARangeMin;
        public double UARangeMax;
        public int UAWriteMask;
        public int UAUserWriteMask;
        public bool UAMandatory;
        public bool AllowWrites;
    }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class OPCUAFolderAttribute : OPCUABaseAttribute
    {
        public new const string Meta = "UAFolder";
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class OPCUAVariableAttribute : OPCUAPropertyAttribute
    {
        public new const string Meta = "UAVariable";
        public bool IsVariable = true;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class OPCUAHAVariableAttribute : OPCUAVariableAttribute
    {
        public new const string Meta = "UAHAVariable";
        public bool Historizing;
        public int MinimumSamplingInterval;
        public int MaximumSamplingInterval;
        public DateTimeOffset ArchiveStart;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class OPCUAMethodAttribute : OPCUABaseAttribute
    {
        public new const string Meta = "UAMethodCommand";
    }
}
