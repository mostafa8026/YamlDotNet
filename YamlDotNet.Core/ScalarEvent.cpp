#include "StdAfx.h"
#include "ScalarEvent.h"
#include "StringConverter.h"
#include "YamlException.h"

namespace YamlDotNet {
	namespace Core {
		ScalarEvent::ScalarEvent(const yaml_event_t* nativeEvent)
			: YamlEvent(nativeEvent)
		{
			style = (ScalarStyle)nativeEvent->data.scalar.style;
			isPlainImplicit = nativeEvent->data.scalar.plain_implicit != 0;
			isQuotedImplicit = nativeEvent->data.scalar.quoted_implicit != 0;
		}

		ScalarEvent::ScalarEvent(String^ _value)
			: value(_value), tag(nullptr), anchor(nullptr), style(ScalarStyle::Plain), isPlainImplicit(true), isQuotedImplicit(true)
		{
		}

		ScalarEvent::ScalarEvent(String^ _value, String^ _tag)
			: value(_value), tag(_tag), anchor(nullptr), style(ScalarStyle::Plain), isPlainImplicit(true), isQuotedImplicit(true)
		{
		}

		ScalarEvent::ScalarEvent(String^ _value, String^ _tag, String^ _anchor)
			: value(_value), tag(_tag), anchor(_anchor), style(ScalarStyle::Plain), isPlainImplicit(true), isQuotedImplicit(true)
		{
		}

		ScalarEvent::ScalarEvent(String^ _value, String^ _tag, String^ _anchor, ScalarStyle _style)
			: value(_value), tag(_tag), anchor(_anchor), style(_style), isPlainImplicit(true), isQuotedImplicit(true)
		{
		}

		ScalarEvent::ScalarEvent(String^ _value, String^ _tag, String^ _anchor, ScalarStyle _style, bool _isPlainImplicit, bool _isQuotedImplicit)
			: value(_value), tag(_tag), anchor(_anchor), style(_style), isPlainImplicit(_isPlainImplicit), isQuotedImplicit(_isQuotedImplicit)
		{
		}

		ScalarEvent::~ScalarEvent()
		{
		}

		String^ ScalarEvent::Anchor::get() {
			if(anchor == nullptr && NativeEvent != NULL) {
				anchor = StringConverter::Convert(NativeEvent->data.scalar.anchor);
			}
			return anchor;
		}

		String^ ScalarEvent::Tag::get() {
			if(tag == nullptr && NativeEvent != NULL) {
				tag = StringConverter::Convert(NativeEvent->data.scalar.tag);
			}
			return tag;
		}
		
		String^ ScalarEvent::Value::get() {
			if(value == nullptr && NativeEvent != NULL) {
				value = StringConverter::Convert(NativeEvent->data.scalar.value);
			}
			return value;
		}

		int ScalarEvent::Length::get() {
			if(NativeEvent != NULL) {
				return NativeEvent->data.scalar.length;
			} else {
				return value->Length;
			}
		}

		bool ScalarEvent::IsPlainImplicit::get() {
			return isPlainImplicit;
		}

		bool ScalarEvent::IsQuotedImplicit::get() {
			return isQuotedImplicit;
		}

		ScalarStyle ScalarEvent::Style::get() {
			return style;
		}

		String^ ScalarEvent::ToString() {
			return String::Format(System::Globalization::CultureInfo::InvariantCulture, 
				"{0} {1} {2} {3} {4} {5} {6} {7}",
				GetType()->Name,
				Anchor,
				Tag,
				Value,
				Length,
				IsPlainImplicit ? "plain_implicit" : "plain_explicit",
				IsPlainImplicit ? "quoted_implicit" : "quoted_explicit",
				Style
			);
		}
	
		void ScalarEvent::CreateEvent(yaml_event_t* nativeEvent) {
			yaml_char_t* anchorBuffer = StringConverter::Convert(Anchor);
			yaml_char_t* tagBuffer = StringConverter::Convert(Tag);
			yaml_char_t* valueBuffer = StringConverter::Convert(Value);

			// TODO: Allow to specify the style?
			int result = yaml_scalar_event_initialize(
				nativeEvent,
				anchorBuffer,
				tagBuffer,
				valueBuffer,
				Value->Length,
				IsPlainImplicit ? 1 : 0,				
				IsQuotedImplicit ? 1 : 0,				
				YAML_ANY_SCALAR_STYLE
			);

			if(valueBuffer != NULL) {
				delete[] valueBuffer;
			}
			if(tagBuffer != NULL) {
				delete[] tagBuffer;
			}
			if(anchorBuffer != NULL) {
				delete[] anchorBuffer;
			}

			if(result != 1) {
				throw gcnew YamlException();
			}
		}
	}
}