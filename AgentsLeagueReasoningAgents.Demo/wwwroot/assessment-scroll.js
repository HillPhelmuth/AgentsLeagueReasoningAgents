window.assessmentScroll = {
	scrollToBottom: (element) => {
		if (!element) {
			return;
		}

		element.scrollTop = element.scrollHeight;
	}
};
